# 截圖驗收工具（零依賴，純 Python + zlib 解 PNG）
# 用途：在無法目視時，用像素證據驗證 2D 畫面真的畫出該有的東西。
#
#   python game2d/tools/inspect_shot.py shots2d/cf2d_0.png shots2d/cf2d_2.png
#
# 檢查項目：
#   1. 地形底色是否出現（星球 0 = 偏綠）
#   2. 各建築色塊 / 出貨綠箭頭 / 進料藍箭頭 / 帶面與 V 形條紋是否出現
#   3. 左手上角的 HUD 文字是否真的有亮點（證明有畫字）
#   4. 兩張不同時間點的截圖是否有差異（證明模擬在推進、動畫在動）
import sys
import zlib
import struct
import collections

# 目標色（與 src/View2D/View/Palette2D.cs 對照），容許小誤差
TARGETS = [
    ("Miner 黃",        (0xC8, 0xA0, 0x50)),
    ("Smelter 橘紅",    (0xB0, 0x55, 0x3A)),
    ("Depot 白灰",      (0xC8, 0xC8, 0xD0)),
    ("SolarPanel 藍",   (0x3A, 0x6F, 0xB0)),
    ("Battery 綠",      (0x6A, 0xB0, 0x6A)),
    ("Turret 紅",       (0xB0, 0x3A, 0x3A)),
    ("出貨箭頭 綠",     (0x40, 0xF2, 0x59)),
    ("進料箭頭 藍",     (0x4C, 0x99, 0xFF)),
    ("帶面底 深灰",     (0x29, 0x2B, 0x33)),
    ("V 形條紋 灰",     (0x61, 0x66, 0x75)),
    ("蟲潮 紅",         (0xD9, 0x40, 0x4D)),
    ("游標高亮 亮綠",   (0x66, 0xFF, 0x80)),
    ("鐵礦 棕",         (0x8A, 0x6A, 0x50)),
    ("銅礦 橙",         (0xC8, 0x7F, 0x3A)),
]


def load_png(path):
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit("not a png: " + path)

    pos, idat, w, h, bd, ct = 8, b"", 0, 0, 0, 0
    while pos < len(data):
        (ln,) = struct.unpack(">I", data[pos:pos + 4])
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if typ == b"IHDR":
            w, h, bd, ct, _c, _f, _i = struct.unpack(">IIBBBBB", body)
        elif typ == b"IDAT":
            idat += body
        elif typ == b"IEND":
            break

    if bd != 8:
        raise SystemExit("unsupported bit depth " + str(bd))
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ct]
    bpp = ch
    stride = w * bpp
    raw = zlib.decompress(idat)

    out = bytearray()
    prev = bytearray(stride)
    p = 0
    for _y in range(h):
        f = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        if f == 0:
            pass
        elif f == 1:  # Sub
            for c in range(bpp):
                acc = 0
                for i in range(c, stride, bpp):
                    acc = (acc + line[i]) & 255
                    line[i] = acc
        elif f == 2:  # Up
            line = bytearray((line[i] + prev[i]) & 255 for i in range(stride))
        elif f == 3:  # Average
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 255
        elif f == 4:  # Paeth
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        else:
            raise SystemExit("bad filter " + str(f))
        out += line
        prev = line
    return w, h, ch, bytes(out)


def quantized_hist(w, h, ch, px):
    hist = collections.Counter()
    for i in range(0, w * h * ch, ch):
        r, g, b = px[i], px[i + 1], px[i + 2]
        hist[(r >> 3, g >> 3, b >> 3)] += 1
    return hist


def near(hist, target, tol=1):
    tr, tg, tb = target
    n = 0
    for dr in range(-tol, tol + 1):
        for dg in range(-tol, tol + 1):
            for db in range(-tol, tol + 1):
                n += hist.get(((tr >> 3) + dr, (tg >> 3) + dg, (tb >> 3) + db), 0)
    return n


def bright_pixels_in(w, h, ch, px, x0, y0, x1, y1, threshold=200):
    n = 0
    for y in range(y0, min(y1, h)):
        row = y * w * ch
        for x in range(x0, min(x1, w)):
            i = row + x * ch
            if px[i] > threshold and px[i + 1] > threshold and px[i + 2] > threshold:
                n += 1
    return n


def diff_pixels(a, b):
    n = 0
    total = min(len(a), len(b))
    for i in range(0, total, 7):
        if abs(a[i] - b[i]) > 10:
            n += 1
    return n


def report(path):
    w, h, ch, px = load_png(path)
    print(f"── {path}  ({w}x{h}, {ch}ch) ──")
    hist = quantized_hist(w, h, ch, px)

    print("  建築/介面色塊（像素數，>0 表示有畫出來）：")
    found = 0
    for name, rgb in TARGETS:
        n = near(hist, rgb)
        mark = "OK " if n > 0 else "-- "
        if n > 0:
            found += 1
        print(f"    {mark}{name:<16} {n:>7}")
    print(f"    → {found}/{len(TARGETS)} 命中")

    greenish = sum(c for (r, g, b), c in hist.items() if g > r + 1 and g > b + 1)
    print(f"  地形（偏綠像素）: {greenish}  → {'OK' if greenish > 1000 else 'MISSING'}")

    hud = bright_pixels_in(w, h, ch, px, 8, 6, 520, 110)
    print(f"  HUD 文字亮點（左上角）: {hud}  → {'OK' if hud > 50 else 'MISSING'}")

    uniq = len(hist)
    print(f"  相異色階數: {uniq}")
    top = ", ".join(f"#{r*8},{g*8},{b*8}×{c}" for (r, g, b), c in hist.most_common(5))
    print(f"  主色: {top}")
    return w, h, ch, px


def main():
    if len(sys.argv) < 2:
        raise SystemExit("usage: inspect_shot.py shot.png [shot2.png ...]")

    imgs = []
    for p in sys.argv[1:]:
        imgs.append(report(p))

    if len(imgs) >= 2:
        a, b = imgs[0][3], imgs[-1][3]
        d = diff_pixels(a, b)
        print(f"── 第一張 vs 最後一張 差異像素（抽樣）: {d} → {'OK 模擬/動畫在推進' if d > 200 else 'SUSPICIOUS 畫面沒變化'}")


if __name__ == "__main__":
    main()
