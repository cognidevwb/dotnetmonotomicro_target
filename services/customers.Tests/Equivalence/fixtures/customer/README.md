# Golden-master fixtures

Each JSON file here is a `{ request, seed, expectedResponse }` golden derived from the
PORTED legacy action for this aggregate. `GoldenReplay` boots the new service, replays
`request` against a seeded database, `JsonNormalize`s away volatile fields, and asserts
the response equals `expectedResponse` — proving the new service behaves like the
legacy monolith. Goldens can also be RECORDED from the running monolith (see the
equivalence develop task).
