# Recorded catalog responses

Drop one JSON file per representative `catalog` API response here (the exact body
this service's typed `catalog` client must be able to bind). The contract test
asserts each satisfies the read-model via `SchemaGuard.AssertResponseSatisfies`, so a
breaking change to `catalog`'s shape fails THIS service's build.
