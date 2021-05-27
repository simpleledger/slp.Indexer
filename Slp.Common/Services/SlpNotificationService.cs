using Slp.Common.Extensions;
using Slp.Common.Models;
using Slp.Common.Models.DbModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Altcoins;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

using Slp.Common.Interfaces;
using Slp.Common.Utility;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace Slp.Common.Services
{
    public class SlpNotificationService : ISlpNotificationService
    {
        private const string HashTx = "hashtx";
        private const string HashBlock = "hashblock";
        private const string RawTx = "rawtx";
        private const string RawBlock = "rawblock";

        private readonly ILogger<SlpNotificationService> _log;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;


        SubscriberSocket _subscriberSocket;
        PublisherSocket _publisherSocket;
        private NetMQPoller _poller;
        readonly ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
        Network _network;
        public SlpNotificationService(
           IConfiguration configuration,
           HttpClient httpClient,
           ILogger<SlpNotificationService> log)
        {
            _configuration = configuration;
            _log = log;
            _httpClient = httpClient;

            var nodeType = _configuration.GetValue(nameof(SD.BchNodeType), SD.BchNodeType);
            //if (!Enum.TryParse<NetworkType>(nodeType, true, out var networkType))
            //    throw new Exception($"Failed to parse {nodeType} to NetworkType value!");
            _network = NBitcoin.Altcoins.BCash.Instance.GetNetwork(new ChainName(nodeType));
        }

        public event Action<Block> OnNewBlock;
        public event Action<Transaction> OnNewTransaction;

        bool _exit = false;
        public async Task ProcessEventsAsync()
        {
            while (!_exit || _actionQueue.Any() )
            {
                if (!_actionQueue.Any())
                {
                    await Task.Delay(250);
                    continue;
                }
                if (!_actionQueue.TryDequeue(out Action action))
                {
                    await Task.Delay(10);
                    continue;
                }
                action.Invoke();
            }
        }

        public Task RunAsync()
        {
            return Task.Run(() =>
            {
                _log.LogInformation("Establishing zmq notifications...");
                _ = Task.Run(() =>
                {
                    var zmqPublishAddress = _configuration.GetValue(nameof(SD.ZmqPublishAddress), SD.ZmqPublishAddress);
                    if (!string.IsNullOrEmpty(zmqPublishAddress))
                    {
                        _log.LogInformation("Establishing publisher at {0}", zmqPublishAddress);
                        _publisherSocket = new PublisherSocket(zmqPublishAddress);
                        _publisherSocket.Connect(zmqPublishAddress);
                        _log.LogInformation("Publisher connected at {0}", zmqPublishAddress);
                    }
                });
                
                _log.LogInformation("Starting message processing loop...");
                _ = Task.Run(ProcessEventsAsync);

                _ = Task.Run(() => {
                    var zmqSubscribeAddress = _configuration.GetValue(nameof(SD.ZmqSubscribeAddress), SD.ZmqSubscribeAddress);
                    _log.LogInformation("Establishing subscriber at {0}", zmqSubscribeAddress);
                    using (_subscriberSocket = new SubscriberSocket())
                    {
                        _subscriberSocket.Subscribe("");
                        _subscriberSocket.Connect(zmqSubscribeAddress);
                        _log.LogInformation("Subscriber connected at {0}", zmqSubscribeAddress);

                        // listen to incoming messages from other publishers, forward them to the shim
                        _subscriberSocket.ReceiveReady += OnSubscriberReady;
                        // Create and configure the poller with all sockets and the timer
                        _poller = new NetMQPoller { _subscriberSocket };

                        // polling until cancelled
                        _poller.Run();
                    }
                    _exit = true;
                });
                
            });
        }
        public void NotifySlpTransaction(SlpTransaction slpTransaction)
        {
            var webhook = _configuration.GetValue(nameof(SD.NotificationWebhookUrl), SD.NotificationWebhookUrl);
            if (!string.IsNullOrEmpty(webhook))
            {
                _actionQueue.Enqueue(
                    async () =>
                    {
                        try
                        {
                            _log.LogInformation("Notifying new transaction via webhook {0}", webhook);

                            var slpAsJson = JsonConvert.SerializeObject(slpTransaction);
                            var res = await _httpClient.PostAsync(webhook, new StringContent(slpAsJson, Encoding.UTF8));
                            if (!res.IsSuccessStatusCode)
                            {
                                _log.LogError("Failed to notify listener via webhook {0} about transaction {1}", webhook, slpTransaction.Hash.ToHex());
                            }
                        }
                        catch (Exception e)
                        {
                            _log.LogError("Notify listener via web hook failed with: " + e.Message);
                            _log.LogError(e.StackTrace);
                        }
                    });
            }

            if (_publisherSocket != null)
            {
                _actionQueue.Enqueue(
                    () =>
                    {
                        _log.LogInformation("Publish slp transaction added to db: {0}", slpTransaction);
                        _publisherSocket.SendMoreFrame(nameof(SlpTransaction)).SendFrame(slpTransaction.Hash.ToHex());
                    });
            }
        }

        public void NotifyHeartBeat(int progress)
        {
            _actionQueue.Enqueue(
                   () =>
                   {
                       // _log.LogDebug("HeartBeat: "  + progress);
                       _publisherSocket?.SendMoreFrame("HeartBeat").SendFrame(progress.ToString());
                   });
        }

        public void NotifySlpBlock(SlpBlock block)
        {
            var webhook = _configuration.GetValue(nameof(SD.NotificationWebhookUrl), SD.NotificationWebhookUrl);
            if (!string.IsNullOrEmpty(webhook))
            {
                _actionQueue.Enqueue(
                    async () =>
                    {
                        try
                        {
                            _log.LogInformation("Notifying new transaction via webhook {0}", webhook);

                            var slpAsJson = JsonConvert.SerializeObject(block);
                            var res = await _httpClient.PostAsync(webhook, new StringContent(slpAsJson, Encoding.UTF8));
                            if (!res.IsSuccessStatusCode)
                            {
                                _log.LogError("Failed to notify listener via webhook {0} about transaction {1}", webhook, block.Hash.ToHex());
                            }
                        }
                        catch (Exception e)
                        {
                            _log.LogError("Notify listener via web hook failed with: " + e.Message);
                            _log.LogError(e.StackTrace);
                        }
                    });
            }

            if (_publisherSocket != null)
            {
                _actionQueue.Enqueue(
                    () =>
                    {
                        _log.LogInformation("Publishing on new slp block added to db: {0}", block.Hash.ToHex());
                        _publisherSocket.SendMoreFrame(nameof(SlpBlock)).SendFrame(block.Hash.ToHex());
                    });
            }
        }


        private void OnSubscriberReady(object sender, NetMQSocketEventArgs e)
        {
            var topic = _subscriberSocket.ReceiveFrameString();
            switch (topic)
            {                
                case RawBlock:
                    {
                        
                        var msg = _subscriberSocket.ReceiveFrameBytes();
                        //do not process on notification event but add function that will perform processing on EventProcessor thread
                        _actionQueue.Enqueue(() =>
                        {
                            var block = Block.Load(msg, _network);
                            OnNewBlock?.Invoke(block);
                        });
                        break;
                    }
                case RawTx:
                    {
                        var msg = _subscriberSocket.ReceiveFrameBytes();
                        //do not process on notification event but add function that will perform processing on EventProcessor thread
                        _actionQueue.Enqueue(() =>
                        {
                            var tr = Transaction.Load(msg, _network);
                            OnNewTransaction?.Invoke(tr);                            
                        }
                        );
                        break;
                    }
                case HashBlock:
                        break;
                case HashTx:
                        break;
                default:
                    break;
            };
        }    
    }
}
