# Recorded customers responses

Drop one JSON file per representative `customers` API response here (the exact body
this service's typed `customers` client must be able to bind). The contract test
asserts each satisfies the read-model via `SchemaGuard.AssertResponseSatisfies`, so a
breaking change to `customers`'s shape fails THIS service's build.
