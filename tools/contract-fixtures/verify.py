#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import re
import tomllib
from pathlib import Path

from mv_dcbor import domain_hash, encode
from persistence import current_pointer, u64be

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
    if kind == "uint": return vector["value"]
    if kind == "negative": return vector["value"]
    if kind == "bytes": return bytes.fromhex(vector["value_hex"])
    if kind == "text": return vector["value"]
    if kind == "bool": return vector["value"]
    if kind == "null": return None
    if kind == "array_uint": return vector["value"]
    if kind == "map_uint_text": return {int(key): value for key, value in vector["value"].items()}
    raise AssertionError(f"unknown fixture kind: {kind}")


def verify_mv_dcbor() -> None:
    data = load("mv-dcbor.json")
    for vector in data["vectors"]:
        actual = encode(decode_fixture_value(vector)).hex()
        assert actual == vector["encoded_hex"], vector["name"]
    for vector in data["domain_hash_vectors"]:
        context = vector["context"]
        value = {0: bytes.fromhex(context["id128_hex"]), 1: context["step"], 2: context["token"]}
        assert encode(value).hex() == vector["encoded_hex"], vector["name"]
        assert domain_hash(vector["label"], value).hex() == vector["digest_hex"], vector["name"]


def verify_identity_derivation() -> None:
    data = load("identity-derivation.json")
    for vector in data["entity_id"]:
        context = vector["context"]
        value = {0: bytes.fromhex(context["world_id_hex"]), 1: context["creation_step"], 2: context["creator_domain"], 3: bytes.fromhex(context["creator_entity_id_hex"]), 4: context["creation_kind"], 5: context["local_ordinal"], 6: context["nonce"]}
        digest = domain_hash(vector["label"], value)
        assert encode(value).hex() == vector["encoded_hex"], vector["name"]
        assert digest.hex() == vector["domain_hash_hex"], vector["name"]
        assert digest[:16].hex() == vector["id128_hex"], vector["name"]
    for vector in data["intent_id"]:
        context = vector["context"]
        value = {0: bytes.fromhex(context["world_id_hex"]), 1: context["effective_step"], 2: context["source_kind"], 3: bytes.fromhex(context["source_id_hex"]), 4: context["domain"], 5: context["mutation_kind"], 6: context["local_ordinal"]}
        digest = domain_hash(vector["label"], value)
        assert encode(value).hex() == vector["encoded_hex"], vector["name"]
        assert digest.hex() == vector["domain_hash_hex"], vector["name"]
        assert digest[:16].hex() == vector["id128_hex"], vector["name"]


def verify_random() -> None:
    for vector in load("random.json")["random_word64"]:
        context = {int(key): value for key, value in vector["context"].items()}
        value = {0: bytes.fromhex(vector["world_seed_hex"]), 1: context, 2: vector["draw_index"], 3: vector["retry_index"]}
        digest = domain_hash(vector["label"], value)
        assert encode(value).hex() == vector["encoded_outer_hex"], vector["name"]
        assert digest.hex() == vector["domain_hash_hex"], vector["name"]
        assert int.from_bytes(digest[:8], "big") == vector["word64"], vector["name"]


def verify_order() -> None:
    data = load("order.json")["same_step_order"]
    ordered = sorted(data["items"], key=lambda item: (item["phase"], item["domain_rank"], bytes.fromhex(item["conflict_scope_digest_hex"]), item["semantic_priority"], bytes.fromhex(item["intent_id_hex"])))
    assert [item["name"] for item in ordered] == data["expected_order"]


def verify_config_examples() -> None:
    source = (ROOT / "docs" / "design" / "phase4-config-standard-examples.md").read_text(encoding="utf-8")
    blocks = re.findall(r"```toml\n(.*?)```", source, flags=re.DOTALL)
    assert len(blocks) == 4, f"expected 4 standard TOML examples, got {len(blocks)}"
    parsed = [tomllib.loads(block) for block in blocks]
    assert [document["meta"]["component"] for document in parsed] == ["simulation-core", "gateway", "general-view", "admin-view"]
    for document in parsed:
        assert document["meta"]["format"] == "machiverse-config"
        assert document["meta"]["schema_version"] == "1.0"


def verify_persistence() -> None:
    data = load("persistence.json")
    for vector in data["u64be"]:
        assert u64be(vector["value"]).hex() == vector["hex"]
    for vector in data["current_pointer"]:
        actual = current_pointer(vector["generation"])
        assert actual.decode("ascii") == vector["ascii"]
        assert len(actual) == vector["length"] == 17


def read_varint(data: bytes, offset: int) -> tuple[int, int]:
    value = 0
    shift = 0
    while True:
        byte = data[offset]
        offset += 1
        value |= (byte & 0x7F) << shift
        if byte < 0x80:
            return value, offset
        shift += 7
        assert shift < 70


def read_fields(data: bytes) -> list[tuple[int, int, object]]:
    fields = []
    offset = 0
    while offset < len(data):
        tag, offset = read_varint(data, offset)
        field_number, wire_type = tag >> 3, tag & 7
        if wire_type == 0:
            value, offset = read_varint(data, offset)
        elif wire_type == 2:
            length, offset = read_varint(data, offset)
            value = data[offset:offset + length]
            offset += length
        else:
            raise AssertionError(f"unsupported fixture wire type {wire_type}")
        fields.append((field_number, wire_type, value))
    return fields


def verify_protobuf() -> None:
    for vector in load("protobuf.json")["protocol_hello"]:
        fields = read_fields(bytes.fromhex(vector["hex"]))
        values = {(number, index): value for index, (number, _wire, value) in enumerate(fields)}
        assert fields[0][0] == 1 and fields[0][2].decode() == vector["protocol_id"]
        assert fields[1][0] == 2
        nested = read_fields(fields[1][2])
        assert nested == [(1, 0, vector["supported_major"])]
        assert fields[2][0] == 3 and fields[2][2].decode() == vector["provided_capability"]
        assert fields[3][0] == 4 and fields[3][2].decode() == vector["required_capability"]
        assert len(values) == 4


def main() -> None:
    manifest = load("manifest.json")
    assert manifest["fixture_format"] == "machiverse-contract-fixtures"
    assert manifest["version"] == 1
    verify_stable_tokens(); verify_fixed_width(); verify_sha256(); verify_mv_dcbor()
    verify_identity_derivation(); verify_random(); verify_order(); verify_config_examples()
    verify_persistence(); verify_protobuf()
    print("contract fixtures v1: PASS")


if __name__ == "__main__":
    main()
