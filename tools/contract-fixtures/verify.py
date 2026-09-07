#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from mv_dcbor import domain_hash, encode

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / "tests" / "contract-fixtures" / "v1"
TOKEN_RE = re.compile(r"^[a-z0-9][a-z0-9._/-]{0,63}$", re.ASCII)


def load(name: str):
    return json.loads((FIXTURES / name).read_text(encoding="utf-8"))


def verify_stable_tokens() -> None:
    data = load("stable-token.json")
    for value in data["valid"]:
        assert TOKEN_RE.fullmatch(value), f"expected valid StableToken: {value!r}"
    for value in data["invalid"]:
        assert not TOKEN_RE.fullmatch(value), f"expected invalid StableToken: {value!r}"


def verify_fixed_width() -> None:
    data = load("id128-hash256.json")
    for value in data["id128_valid_hex"]:
        assert len(bytes.fromhex(value)) == 16
    for value in data["hash256_valid_hex"]:
        assert len(bytes.fromhex(value)) == 32


def verify_sha256() -> None:
    data = load("id128-hash256.json")
    for vector in data["sha256"]:
        actual = hashlib.sha256(bytes.fromhex(vector["input_hex"])).hexdigest()
        assert actual == vector["digest_hex"], vector["name"]


def decode_fixture_value(vector: dict):
    kind = vector["kind"]
    if kind == "uint":
        return vector["value"]
    if kind == "bytes":
        return bytes.fromhex(vector["value_hex"])
    if kind == "text":
        return vector["value"]
    if kind == "array_uint":
        return vector["value"]
    if kind == "map_uint_text":
        return {int(key): value for key, value in vector["value"].items()}
    raise AssertionError(f"unknown fixture kind: {kind}")


def verify_mv_dcbor() -> None:
    data = load("mv-dcbor.json")
    for vector in data["vectors"]:
        actual = encode(decode_fixture_value(vector)).hex()
        assert actual == vector["encoded_hex"], vector["name"]

    for vector in data["domain_hash_vectors"]:
        context = vector["context"]
        value = {
            0: bytes.fromhex(context["id128_hex"]),
            1: context["step"],
            2: context["token"]
        }
        assert encode(value).hex() == vector["encoded_hex"], vector["name"]
        assert domain_hash(vector["label"], value).hex() == vector["digest_hex"], vector["name"]


def main() -> None:
    manifest = load("manifest.json")
    assert manifest["fixture_format"] == "machiverse-contract-fixtures"
    assert manifest["version"] == 1
    verify_stable_tokens()
    verify_fixed_width()
    verify_sha256()
    verify_mv_dcbor()
    print("contract fixtures v1: PASS")


if __name__ == "__main__":
    main()
