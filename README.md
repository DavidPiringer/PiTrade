# PiTrade
PiTrade is a hobby/fun project to try automated trading on a raspberry pi. It's still in early development and the codebase (interfaces, implementation, architecture, ...) can change vastly.  

## Risk Warning
If you want to use PiTrade keep in mind you **USE IT ONLY AT YOUR OWN RISK**. I don't take any responsibility for financial losses caused by this software.

## Current TODOS
* add command line interface
* refactor exchange loop / rethink exchange <-> market <-> order architecture
* add additional order options (e.g. stop loss)
* add unit tests and a possiblity to mock an exchange
* add documentation/comments/examples (only when core parts are considered "finished" for me)
* add control mechanism for active trading strategies
  * maybe with a fancy web ui?
  * maybe as a daemon service?
* add a persistence mechanic 
