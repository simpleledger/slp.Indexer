# Slp.Indexer - Slp indexer and web api solution implemented in .NET Core using C#

## `Slp.Indexer`: SLP indexer .net core console app

This is a .NET Core [SLP](https://slp.dev/) fast indexer implementation written in C#. 

The tool runs as a console application that query blocks using NBitcoin RPCClient from BCH Node, process its transactions by checking for SLP OP_RETURN data, validates transactions and then writes data into backend database(currently SQL Server). When tool reaches chain-head it switches to realtime mempool indexing by subscribing to BCH node ZMQ and processing new transactions and blocks and handling reorgs.

This tool was developed by rewriting a lot of code from [SLPDB](https://github.com/simpleledger/SLPDB), [SLPJS](https://github.com/simpleledger/slpjs) and [SLPVALIDATE](https://github.com/simpleledger/slp-validate.js). 

Database schema is defined by EntityFrameworkCore's Code First approach. Current implementation was developed and tested using Microsoft Sql Server as a backend. 

Database schema is designed so it uses as little data as possible. Currently SLP mainnet database needs less than 6 GB of space( May 2021 ).

### Limitations and Issues

Current version relies heavily on RAM as it parses all transactions in RAM so SLP validations are very fast. Since database schema is very compact this can be easily managed by adding more RAM if network will grow. Currently no pruning is implemented so there is also room for RAM usage optimization there.

### Configuration options

You can run the tool by setting proper options in appsettings.json.
* StartFromBlock: defines from which block the tool should start reading with empty database. 543375 for SLP Mainent, 1253800 for SLP testnet, 0 for BCH mainnet and testnet.
* DbCommitBatchSize: - how many transactions should be indexed before written to backend database. This value is determined by database server capabilities.
* RPCWorkerCount: number of parallel workers that fetch block data from BCH RPC Node.
* RPCBlockPrefetchLimit: number of blocks that will be read ahead. If block reading is faster than database writting then we set limit here so we do not use too much RAM.
* ZmqSubscribeAddress: BCH Node ZMQ address. When indexer reaches mempool it listens for new transactions and blocks on this address.
* ZmqPublishAddresss: Slp.Indexer publishes new SLP transactions on its own NetMQ(ZMQ) port that clients can subscribe to.
* ConnectionStrings: SlpDbConnection connection to backed database
* BchNode: bch node connection data needs to specify user, password, url and type ( Mainnet, Testnet )

### Installation

Thre is no installation needed for this tool other than compiling, setting proper appsettings and running it. It can be be deployed however also as a Windows or a Linux service(i.e. systemd).

## `Slp.API`: SLP REST API

.NET core web api project that implements different APIs that provide SLP information.

Rest API implementation in C# that uses database produced by Slp.Indexer and data from BCH node.
All APIs are compatible with obsolete SLP endpoints [Bitcoin.com REST V2](https://rest.bitcoin.com/)

You can check and test running version hosted by [EligmaLabs](https://eligmalabs.com) at [BCH.API](https://slp-indexer.eligmalabs.com/swagger/index.html).

### Configuration options
* ConnectionStrings: SlpDbConnection - connectiong string to database produced by Slp.Indexer
* BchNode: bch node connection data needs to specify user, password, url and type ( Mainnet, Testnet )
* There are also rate limiting options that are described in detail at [AspNetCoreRateLimit](https://github.com/stefanprodan/AspNetCoreRateLimit)

### Installation
This web application it can be easily deployed to any .NET core hosting provider that has access to Slp.Indexer database and Bch node via RPC.

### Limitations and Issues
Currently only SLP APIs are supported.

