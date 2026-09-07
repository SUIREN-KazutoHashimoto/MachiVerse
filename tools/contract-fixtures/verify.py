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


def verify_identity_derivation() -> None:
    data = load("identity-derivation.json")

    for vector in data["entity_id"]:
        context = vector["context"]
        value = {
            0: bytes.fromhex(context["world_id_hex"]),
            1: context["creation_step"],
            2: context["creator_domain"],
            3: bytes.fromhex(context["creator_entity_id_hex"]),
            4: context["creation_kind"],
            5: context["local_ordinal"],
            6: context["nonce"]
        }
        encoded = encode(value)
        digest = domain_hash(vector["label"], value)
        assert encoded.hex() == vector["encoded_hex"], vector["name"]
        assert digest.hex() == vector["domain_hash_hex"], vector["name"]
        assert digest[:16].hex() == vector["id128_hex"], vector["name"]

    for vector in data["intent_id"]:
        context = vector["context"]
        value = {
            0: bytes.fromhex(context["world_id_hex"]),
            1: context["effective_step"],
            2: context["source_kind"],
            3: bytes.fromhex(context["source_id_hex"]),
            4: context["domain"],
            5: context["mutation_kind"],
            6: context["local_ordinal"]
        }
        encoded = encode(value)
        digest = domain_hash(vector["label"], value)
        assert encoded.hex() == vector["encoded_hex"], vector["name"]
        assert digest.hex() == vector["domain_hash_hex"], vector["name"]
        assert digest[:16].hex() == vector["id128_hex"], vector["name"]


def verify_random() -> None:
    data = load("random.json")
    for vector in data["random_word64"]:
        context = {int(key): value for key, value in vector["context"].items()}
        value = {
            0: bytes.fromhex(vector["world_seed_hex"]),
            1: context,
            2: vector["draw_index"],
            3: vector["retry_index"]
        }
        encoded = encode(value)
        digest = domain_hash(vector["label"], value)
        word = int.from_bytes(digest[:8], "big")
        assert encoded.hex() == vector["encoded_outer_hex"], vector["name"]
        assert digest.hex() == vector["domain_hash_hex"], vector["name"]
        assert word == vector["word64"], vector["name"]


def main() -> None:
    manifest = load("manifest.json")
    assert manifest["fixture_format"] == "machiverse-contract-fixtures"
    assert manifest["version"] == 1
    verify_stable_tokens()
    verify_fixed_width()
    verify_sha256()
    verify_mv_dcbor()
    verify_identity_derivation()
    verify_random()
    print("contract fixtures v1: PASS")


if __name__ == "__main__":
    main()
