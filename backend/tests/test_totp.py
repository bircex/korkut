"""TOTP correctness — verified against the published RFC 6238 test vectors (Appendix B)."""

import base64

from korkut.api import totp

# RFC 6238 SHA1 seed and its expected 8-digit codes at specific Unix times.
_SEED_B32 = base64.b32encode(b"12345678901234567890").decode()
_VECTORS = {
    59: "94287082",
    1111111109: "07081804",
    1111111111: "14050471",
    1234567890: "89005924",
    2000000000: "69279037",
    20000000000: "65353130",
}


def test_rfc6238_vectors():
    for t, expected in _VECTORS.items():
        counter = totp.verify(_SEED_B32, expected, timestamp=t, window=0, digits=8)
        assert counter == t // 30, f"T={t} expected counter {t // 30}, got {counter}"


def test_wrong_code_rejected():
    assert totp.verify(_SEED_B32, "00000000", timestamp=59, window=0, digits=8) is None


def test_non_numeric_and_wrong_length_rejected():
    assert totp.verify(_SEED_B32, "abc", timestamp=59) is None
    assert totp.verify(_SEED_B32, "1234567", timestamp=59) is None  # wrong length for 6-digit


def test_window_tolerates_skew():
    # A code from the previous step is accepted with window=1, rejected with window=0.
    secret = totp.new_secret()
    t = 1_000_000_000
    prev_code = totp._hotp(secret, totp.counter_at(t) - 1)
    assert totp.verify(secret, prev_code, timestamp=t, window=1) is not None
    assert totp.verify(secret, prev_code, timestamp=t, window=0) is None


def test_provisioning_uri_shape():
    uri = totp.provisioning_uri("ABC234", "avukat", "Demo Hukuk")
    assert uri.startswith("otpauth://totp/Demo%20Hukuk:avukat?")
    assert "secret=ABC234" in uri and "issuer=Demo%20Hukuk" in uri
