import struct

def read(path, n):
    d = open(path, 'rb').read()
    off = struct.unpack_from('<I', d, n * 4)[0]
    size, comp, mode = struct.unpack_from('<IIH', d, off)
    data = d[off + 10: off + 10 + comp]
    if mode == 0:
        return data[:size]
    out = bytearray(); i = 0
    while len(out) < size:
        flags = data[i]; i += 1
        for b in range(8):
            if len(out) >= size: break
            if flags & (1 << b):
                out.append(data[i]); i += 1
            else:
                e = data[i] | data[i + 1] << 8; i += 2
                for k in range((e & 15) + mode + 1): out.append(out[-((e >> 4) + 1)])
    return bytes(out)

