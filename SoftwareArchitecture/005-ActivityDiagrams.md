# Activity Diagrams

Activity diagrams describe the flow of control in a system. They are useful for modeling business processes and workflows.

## Order Matching

This diagram illustrates the workflow of the matching engine when a new order is received.

1.  The process starts when an order is received.
2.  The engine searches the order book for a matching order (e.g., a buy order for an incoming sell order at a compatible price).
3.  If a match is found, a `Trade` is created, and the statuses of the involved orders are updated (e.g., to `Completed` or `PartiallyFilled`). The relevant users are then notified.
4.  If no match is found, the new order is placed in the order book to await a future match.

![Activity Diagrams](Diagrams/ActivityDiagrams.png)
