import math
from PIL import Image, ImageDraw, ImageFont

# Dimensions
# Color palette (Phosphor Cyan-Green on Deep Dark CRT)
BG = (3, 22, 15, 255)
DARK_TINT = (6, 35, 24, 255)
DIM_GREEN = (15, 90, 60, 255)
MID_GREEN = (28, 160, 108, 255)
BRIGHT_CYAN = (0, 255, 170, 255)
HOT_CYAN = (160, 255, 220, 255)

font_path = "C:/Windows/Fonts/consola.ttf"
try:
    font_small = ImageFont.truetype(font_path, 16)
    font_mid = ImageFont.truetype(font_path, 22)
    font_large = ImageFont.truetype(font_path, 32)
except:
    font_small = font_mid = font_large = ImageFont.load_default()

# -------------------------------------------------------------
# 1. SPY DOSSIER AVATAR (360 x 380)
# -------------------------------------------------------------
def create_avatar():
    w, h = 360, 380
    img = Image.new("RGBA", (w, h), BG)
    draw = ImageDraw.Draw(img)

    # Outer border with corner notches
    draw.rectangle([4, 4, w - 5, h - 5], outline=MID_GREEN, width=2)
    draw.rectangle([8, 8, w - 9, h - 9], outline=DIM_GREEN, width=1)

    # Corner brackets (military dossier reticle)
    corner_len = 20
    draw.line([(4, 4), (4 + corner_len, 4)], fill=BRIGHT_CYAN, width=3)
    draw.line([(4, 4), (4, 4 + corner_len)], fill=BRIGHT_CYAN, width=3)
    draw.line([(w - 5, 4), (w - 5 - corner_len, 4)], fill=BRIGHT_CYAN, width=3)
    draw.line([(w - 5, 4), (w - 5, 4 + corner_len)], fill=BRIGHT_CYAN, width=3)
    draw.line([(4, h - 5), (4 + corner_len, h - 5)], fill=BRIGHT_CYAN, width=3)
    draw.line([(4, h - 5), (4, h - 5 - corner_len)], fill=BRIGHT_CYAN, width=3)
    draw.line([(w - 5, h - 5), (w - 5 - corner_len, h - 5)], fill=BRIGHT_CYAN, width=3)
    draw.line([(w - 5, h - 5), (w - 5, h - 5 - corner_len)], fill=BRIGHT_CYAN, width=3)

    # Top header text
    draw.rectangle([10, 10, w - 10, 38], fill=DARK_TINT)
    draw.text((20, 14), "ДОСЬЕ: АГЕНТ «ДЕКОДЕР»", font=font_small, fill=BRIGHT_CYAN)

    # Photo frame area (18, 46) to (w-18, h-50)
    px0, py0, px1, py1 = 20, 46, w - 20, h - 54
    draw.rectangle([px0, py0, px1, py1], outline=MID_GREEN, width=1)

    # Background grid / scanlines inside photo area
    for y in range(py0 + 4, py1, 8):
        draw.line([(px0 + 1, y), (px1 - 1, y)], fill=(5, 38, 26, 255), width=1)
    for x in range(px0 + 20, px1, 30):
        draw.line([(x, py0 + 1), (x, py1 - 1)], fill=(5, 38, 26, 255), width=1)

    # Draw stylized 1-bit spy silhouette (hat, turned collar, dark glasses, trenchcoat)
    cx = (px0 + px1) // 2
    # Fedora hat crown
    hat_top = py0 + 35
    draw.polygon([(cx - 45, hat_top + 35), (cx - 35, hat_top), (cx + 35, hat_top), (cx + 45, hat_top + 35)], fill=MID_GREEN)
    draw.polygon([(cx - 30, hat_top + 33), (cx - 25, hat_top + 5), (cx + 25, hat_top + 5), (cx + 30, hat_top + 33)], fill=HOT_CYAN)
    # Hat ribbon
    draw.rectangle([cx - 42, hat_top + 28, cx + 42, hat_top + 34], fill=BRIGHT_CYAN)
    # Hat brim
    draw.ellipse([cx - 75, hat_top + 28, cx + 75, hat_top + 46], fill=BRIGHT_CYAN)

    # Face & glasses
    face_top = hat_top + 42
    draw.polygon([(cx - 35, face_top), (cx + 35, face_top), (cx + 30, face_top + 55), (cx - 30, face_top + 55)], fill=MID_GREEN)
    # Dark espionage sunglasses
    draw.rectangle([cx - 32, face_top + 12, cx - 6, face_top + 28], fill=DARK_TINT, outline=BRIGHT_CYAN, width=2)
    draw.rectangle([cx + 6, face_top + 12, cx + 32, face_top + 28], fill=DARK_TINT, outline=BRIGHT_CYAN, width=2)
    draw.line([(cx - 6, face_top + 18), (cx + 6, face_top + 18)], fill=BRIGHT_CYAN, width=2)
    # Glint on glasses
    draw.line([(cx - 28, face_top + 15), (cx - 18, face_top + 25)], fill=HOT_CYAN, width=2)
    draw.line([(cx + 10, face_top + 15), (cx + 20, face_top + 25)], fill=HOT_CYAN, width=2)

    # Nose & mouth shadow
    draw.line([(cx, face_top + 28), (cx - 3, face_top + 38), (cx + 2, face_top + 38)], fill=HOT_CYAN, width=2)
    draw.line([(cx - 12, face_top + 46), (cx + 12, face_top + 46)], fill=DIM_GREEN, width=2)

    # Trenchcoat collar popped up
    neck_y = face_top + 55
    draw.polygon([(cx - 30, neck_y), (cx + 30, neck_y), (cx + 20, neck_y + 25), (cx - 20, neck_y + 25)], fill=DIM_GREEN)
    # Left collar flap
    draw.polygon([(cx - 25, neck_y - 10), (cx - 70, neck_y + 40), (cx - 20, neck_y + 70), (cx - 10, neck_y + 30)], fill=BRIGHT_CYAN)
    # Right collar flap
    draw.polygon([(cx + 25, neck_y - 10), (cx + 70, neck_y + 40), (cx + 20, neck_y + 70), (cx + 10, neck_y + 30)], fill=BRIGHT_CYAN)
    # Coat shoulders
    draw.polygon([(cx - 120, py1), (cx - 80, neck_y + 35), (cx + 80, neck_y + 35), (cx + 120, py1)], fill=MID_GREEN)
    draw.line([(cx, neck_y + 35), (cx, py1)], fill=BRIGHT_CYAN, width=2)

    # Crosshair reticle over portrait
    draw.line([(cx, py0 + 20), (cx, py0 + 35)], fill=BRIGHT_CYAN, width=1)
    draw.line([(cx, py1 - 35), (cx, py1 - 20)], fill=BRIGHT_CYAN, width=1)
    draw.line([(px0 + 20, (py0 + py1) // 2), (px0 + 35, (py0 + py1) // 2)], fill=BRIGHT_CYAN, width=1)
    draw.line([(px1 - 35, (py0 + py1) // 2), (px1 - 20, (py0 + py1) // 2)], fill=BRIGHT_CYAN, width=1)

    # Bottom status bar
    draw.rectangle([10, h - 46, w - 10, h - 10], fill=DARK_TINT)
    draw.text((20, h - 38), "СТАТУС: ВНЕДРЕНИЕ НА БАЗУ", font=font_small, fill=HOT_CYAN)

    img.save("Assets/Textures/CRT/SpyDossierAvatar.png")
    print("Avatar saved!")

# -------------------------------------------------------------
# 2. SPY BASE SCHEMATIC (300 x 380)
# -------------------------------------------------------------
def create_schematic():
    w, h = 300, 380
    img = Image.new("RGBA", (w, h), BG)
    draw = ImageDraw.Draw(img)

    # Border
    draw.rectangle([4, 4, w - 5, h - 5], outline=MID_GREEN, width=2)
    draw.rectangle([8, 8, w - 9, h - 9], outline=DIM_GREEN, width=1)

    # Corner brackets
    corner_len = 18
    for (bx, by, dx, dy) in [(4, 4, 1, 1), (w - 5, 4, -1, 1), (4, h - 5, 1, -1), (w - 5, h - 5, -1, -1)]:
        draw.line([(bx, by), (bx + dx * corner_len, by)], fill=BRIGHT_CYAN, width=3)
        draw.line([(bx, by), (bx, by + dy * corner_len)], fill=BRIGHT_CYAN, width=3)

    # Header
    draw.rectangle([10, 10, w - 10, 38], fill=DARK_TINT)
    draw.text((18, 14), "ОБЪЕКТ: БАЗА «ЗЕНИТ»", font=font_small, fill=BRIGHT_CYAN)

    # Area box
    sx0, sy0, sx1, sy1 = 18, 46, w - 18, h - 54
    draw.rectangle([sx0, sy0, sx1, sy1], outline=MID_GREEN, width=1)

    # Background grid
    for y in range(sy0 + 10, sy1, 20):
        draw.line([(sx0 + 1, y), (sx1 - 1, y)], fill=(5, 38, 26, 255), width=1)
    for x in range(sx0 + 10, sx1, 20):
        draw.line([(x, sy0 + 1), (x, sy1 - 1)], fill=(5, 38, 26, 255), width=1)

    # Radar Circle in upper half
    rcx, rcy = (sx0 + sx1) // 2, sy0 + 90
    draw.ellipse([rcx - 65, rcy - 65, rcx + 65, rcy + 65], outline=DIM_GREEN, width=1)
    draw.ellipse([rcx - 45, rcy - 45, rcx + 45, rcy + 45], outline=MID_GREEN, width=1)
    draw.ellipse([rcx - 20, rcy - 20, rcx + 20, rcy + 20], outline=BRIGHT_CYAN, width=1)
    draw.line([(rcx - 70, rcy), (rcx + 70, rcy)], fill=DIM_GREEN, width=1)
    draw.line([(rcx, rcy - 70), (rcx, rcy + 70)], fill=DIM_GREEN, width=1)

    # Radar sweep line
    draw.line([(rcx, rcy), (rcx + 45, rcy - 45)], fill=HOT_CYAN, width=2)
    # Radar blips
    draw.rectangle([rcx + 25, rcy - 30, rcx + 29, rcy - 26], fill=HOT_CYAN)
    draw.rectangle([rcx - 35, rcy + 20, rcx - 31, rcy + 24], fill=BRIGHT_CYAN)

    # Lower section: Underground bunker layout cross section
    by0 = rcy + 80
    draw.text((sx0 + 8, by0), "УРОВЕНЬ -3: ВЫЧИСЛИТЕЛЬ", font=font_small, fill=BRIGHT_CYAN)

    # Bunker rooms layout
    room_y = by0 + 24
    # Corridor
    draw.rectangle([sx0 + 10, room_y + 15, sx1 - 10, room_y + 35], outline=DIM_GREEN, width=1)
    # Room 1 (Tape Storage)
    draw.rectangle([sx0 + 15, room_y - 10, sx0 + 75, room_y + 15], outline=MID_GREEN, width=1)
    draw.text((sx0 + 20, room_y - 6), "ЛЕНТЫ", font=font_small, fill=DIM_GREEN)
    # Room 2 (Active Terminal - Current player position!)
    draw.rectangle([sx0 + 90, room_y - 18, sx0 + 175, room_y + 15], outline=HOT_CYAN, width=2, fill=(10, 50, 35, 255))
    draw.text((sx0 + 96, room_y - 14), "ЭВМ [ВЫ]", font=font_small, fill=HOT_CYAN)
    # Blinking marker
    draw.ellipse([sx0 + 130, room_y + 2, sx0 + 138, room_y + 10], fill=HOT_CYAN)
    # Room 3 (Generator)
    draw.rectangle([sx0 + 190, room_y - 10, sx1 - 15, room_y + 15], outline=MID_GREEN, width=1)
    draw.text((sx0 + 195, room_y - 6), "СИЛА", font=font_small, fill=DIM_GREEN)

    # Radio frequency wave at bottom
    wave_y = sy1 - 25
    pts = []
    for x in range(sx0 + 10, sx1 - 10, 4):
        wy = wave_y + int(math.sin((x - sx0) * 0.15) * 8)
        pts.append((x, wy))
    for i in range(len(pts) - 1):
        draw.line([pts[i], pts[i+1]], fill=HOT_CYAN, width=2)
    draw.text((sx0 + 10, sy1 - 42), "СИГНАЛ: 142.85 MHz // OK", font=font_small, fill=BRIGHT_CYAN)

    # Footer
    draw.rectangle([10, h - 46, w - 10, h - 10], fill=DARK_TINT)
    draw.text((18, h - 38), "СЕКТОР-7 // ТРЕВОГИ НЕТ", font=font_small, fill=HOT_CYAN)

    img.save("Assets/Textures/CRT/SpyBaseRadarSchematic.png")
    print("Schematic saved!")

# -------------------------------------------------------------
# 3. DECODER ARROW LOGO (560 x 380)
# -------------------------------------------------------------
def create_arrow_logo():
    w, h = 560, 380
    img = Image.new("RGBA", (w, h), BG)
    draw = ImageDraw.Draw(img)

    # Border
    draw.rectangle([4, 4, w - 5, h - 5], outline=MID_GREEN, width=2)
    draw.rectangle([8, 8, w - 9, h - 9], outline=DIM_GREEN, width=1)

    # Corner brackets
    corner_len = 20
    for (bx, by, dx, dy) in [(4, 4, 1, 1), (w - 5, 4, -1, 1), (4, h - 5, 1, -1), (w - 5, h - 5, -1, -1)]:
        draw.line([(bx, by), (bx + dx * corner_len, by)], fill=BRIGHT_CYAN, width=3)
        draw.line([(bx, by), (bx, by + dy * corner_len)], fill=BRIGHT_CYAN, width=3)

    # Sub-header: System classification
    draw.text((w // 2 - 130, 24), "/// СИСТЕМА ДЕШИФРАЦИИ ///", font=font_small, fill=MID_GREEN)

    # Top Hero Word: Д Е К О Д Е Р (Geometric blocky vector lettering)
    # We draw large bold vector letters matching the reference aesthetic
    top_y = 65
    title_text = "ДЕКОДЕР"
    try:
        font_logo = ImageFont.truetype(font_path, 64)
    except:
        font_logo = font_large

    # Center text
    bbox = draw.textbbox((0, 0), title_text, font=font_logo)
    tw = bbox[2] - bbox[0]
    tx = (w - tw) // 2

    # Draw glow behind text
    for offset in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
        draw.text((tx + offset[0], top_y + offset[1]), title_text, font=font_logo, fill=DIM_GREEN)
    draw.text((tx, top_y), title_text, font=font_logo, fill=HOT_CYAN)

    # Underline under top word
    draw.line([(tx - 15, top_y + 75), (tx + tw + 15, top_y + 75)], fill=MID_GREEN, width=2)

    # ---------------------------------------------------------
    # CENTER HERO ELEMENT: THE GIANT BIDIRECTIONAL ARROW
    # Matching the reference image: <===================>
    # ---------------------------------------------------------
    arrow_y = h // 2
    arrow_left = 35
    arrow_right = w - 35
    arrow_thickness = 18
    head_size = 38

    # Arrow left tip: (arrow_left, arrow_y)
    # Arrow right tip: (arrow_right, arrow_y)
    
    # Outer glow / shadow of the arrow
    # Double horizontal shaft rails
    rail_top = arrow_y - arrow_thickness // 2
    rail_bot = arrow_y + arrow_thickness // 2

    # Fill center shaft
    draw.rectangle([arrow_left + head_size, rail_top, arrow_right - head_size, rail_bot], fill=MID_GREEN)
    # Bright inner core
    draw.rectangle([arrow_left + head_size + 4, rail_top + 4, arrow_right - head_size - 4, rail_bot - 4], fill=HOT_CYAN)

    # Left Arrowhead polygon
    left_arrow_pts = [
        (arrow_left, arrow_y),
        (arrow_left + head_size, arrow_y - head_size),
        (arrow_left + head_size, rail_top),
        (arrow_left + head_size, rail_bot),
        (arrow_left + head_size, arrow_y + head_size)
    ]
    draw.polygon([(arrow_left, arrow_y), (arrow_left + head_size, arrow_y - head_size), (arrow_left + head_size, arrow_y + head_size)], fill=BRIGHT_CYAN)
    draw.polygon([(arrow_left + 6, arrow_y), (arrow_left + head_size - 4, arrow_y - head_size + 10), (arrow_left + head_size - 4, arrow_y + head_size - 10)], fill=HOT_CYAN)

    # Right Arrowhead polygon
    draw.polygon([(arrow_right, arrow_y), (arrow_right - head_size, arrow_y - head_size), (arrow_right - head_size, arrow_y + head_size)], fill=BRIGHT_CYAN)
    draw.polygon([(arrow_right - 6, arrow_y), (arrow_right - head_size + 4, arrow_y - head_size + 10), (arrow_right - head_size + 4, arrow_y + head_size - 10)], fill=HOT_CYAN)

    # Decorative tech ticks across the arrow shaft
    for x in range(arrow_left + head_size + 15, arrow_right - head_size - 10, 25):
        draw.line([(x, rail_top - 4), (x, rail_bot + 4)], fill=BG, width=3)
        draw.line([(x, rail_top - 3), (x, rail_bot + 3)], fill=HOT_CYAN, width=1)

    # Arrow caption in center
    badge_w = 190
    draw.rectangle([w // 2 - badge_w // 2, arrow_y - 12, w // 2 + badge_w // 2, arrow_y + 12], fill=BG, outline=BRIGHT_CYAN, width=2)
    draw.text((w // 2 - 80, arrow_y - 8), "КАНАЛ СВЯЗИ", font=font_small, fill=HOT_CYAN)

    # ---------------------------------------------------------
    # Bottom Hero Word: П Е Р Е Х В А Т (Matching "ZAG" from reference)
    # ---------------------------------------------------------
    bot_y = arrow_y + 45
    bot_text = "ПЕРЕХВАТ"
    bbox_bot = draw.textbbox((0, 0), bot_text, font=font_logo)
    btw = bbox_bot[2] - bbox_bot[0]
    btx = (w - btw) // 2

    for offset in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
        draw.text((btx + offset[0], bot_y + offset[1]), bot_text, font=font_logo, fill=DIM_GREEN)
    draw.text((btx, bot_y), bot_text, font=font_logo, fill=HOT_CYAN)

    # Overline above bottom word
    draw.line([(btx - 15, bot_y - 8), (btx + btw + 15, bot_y - 8)], fill=MID_GREEN, width=2)

    # Sub-footer: Protocol info
    draw.text((w // 2 - 145, h - 35), "ПРОТОКОЛ: ШИФРООБМЕН ЕС-5061", font=font_small, fill=MID_GREEN)

    img.save("Assets/Textures/CRT/DecoderArrowLogo.png")
    print("Arrow logo saved!")

create_avatar()
create_schematic()
create_arrow_logo()
