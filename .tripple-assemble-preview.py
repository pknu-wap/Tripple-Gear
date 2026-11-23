from pathlib import Path

folder = Path("animation-previews")


def frame_parts(data):
    packed = data[10]
    assert packed & 0x80
    palette_end = 13 + 3 * (2 << (packed & 7))
    start = palette_end
    while data[start] == 0x21:
        start += 2
        while data[start]:
            start += data[start] + 1
        start += 1
    assert data[start] == 0x2C and not data[start + 9] & 0x80
    descriptor = bytearray(data[start : start + 10])
    descriptor[9] |= 0x80 | (packed & 7)
    assert data[-1] == 0x3B
    return descriptor + data[13:palette_end] + data[start + 10 : -1]


for monster in ("melee", "ranged"):
    frames = [folder / f"{monster}-{index}.gif" for index in range(12)]
    header = bytearray(frames[0].read_bytes()[:13])
    header[:6] = b"GIF89a"
    header[10] &= 0x7F
    header.extend(b"\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00")
    for frame in frames:
        header.extend(b"\x21\xf9\x04\x04\x0c\x00\x00\x00")
        header.extend(frame_parts(frame.read_bytes()))
    header.append(0x3B)
    (folder / f"{monster}-animation-preview.gif").write_bytes(header)
    for frame in frames:
        frame.unlink()
