from __future__ import annotations


def u64be(value: int) -> bytes:
    if value < 0 or value > 0xFFFFFFFFFFFFFFFF:
        raise ValueError("U64BE value must fit uint64")
    return value.to_bytes(8, "big")


def current_pointer(generation: int) -> bytes:
    if generation < 1 or generation > 0xFFFFFFFFFFFFFFFF:
        raise ValueError("PersistenceGeneration must be 1..2^64-1")
    return f"{generation:016x}\n".encode("ascii")
