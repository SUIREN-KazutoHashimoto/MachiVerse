from __future__ import annotations

import hashlib
from typing import Any


def _head(major: int, value: int) -> bytes:
    if value < 0:
        raise ValueError("negative value is not valid for this helper")
    if value < 24:
        return bytes([(major << 5) | value])
    if value <= 0xFF:
        return bytes([(major << 5) | 24, value])
    if value <= 0xFFFF:
        return bytes([(major << 5) | 25]) + value.to_bytes(2, "big")
    if value <= 0xFFFFFFFF:
        return bytes([(major << 5) | 26]) + value.to_bytes(4, "big")
    if value <= 0xFFFFFFFFFFFFFFFF:
        return bytes([(major << 5) | 27]) + value.to_bytes(8, "big")
    raise ValueError("MV-DCBOR v1 unsigned integer exceeds uint64")


def encode(value: Any) -> bytes:
    if isinstance(value, bool):
        return b"\xf5" if value else b"\xf4"
    if isinstance(value, int):
        if value >= 0:
            return _head(0, value)
        n = -1 - value
        return _head(1, n)
    if isinstance(value, bytes):
        return _head(2, len(value)) + value
    if isinstance(value, str):
        encoded = value.encode("ascii")
        return _head(3, len(encoded)) + encoded
    if isinstance(value, list):
        return _head(4, len(value)) + b"".join(encode(item) for item in value)
    if isinstance(value, dict):
        encoded_items = [(encode(key), encode(item)) for key, item in value.items()]
        encoded_items.sort(key=lambda pair: pair[0])
        return _head(5, len(encoded_items)) + b"".join(key + item for key, item in encoded_items)
    if value is None:
        return b"\xf6"
    raise TypeError(f"unsupported MV-DCBOR fixture type: {type(value)!r}")


def domain_hash(label: str, value: Any) -> bytes:
    label_bytes = label.encode("ascii")
    return hashlib.sha256(label_bytes + b"\x00" + encode(value)).digest()
