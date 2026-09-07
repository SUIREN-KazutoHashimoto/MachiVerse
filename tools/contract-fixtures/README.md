# MachiVerse Contract Fixtures

`QA-01` の契約fixture / golden vector基盤です。

production componentのDLLやinternal typeには依存せず、version-controlled dataとstandalone verifierだけで契約を検証します。

現在の初期セット:

- StableToken lexical contract
- Id128 / Hash256 fixed width contract
- SHA-256 reference vectors
- fixture manifest / versioning

MV-DCBOR、protobuf、Config、persistence fixtureは同じ `QA-01` の後続incrementで追加します。
