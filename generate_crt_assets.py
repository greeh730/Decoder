import math
from PIL import Image, ImageDraw, ImageFont

# Dimensions & Authentic Monochrome Phosphor Palette
BG = (3, 22, 15, 255)
DARK_TINT = (6, 35, 24, 255)
DIM_GREEN = (15, 90, 60, 255)
MID_GREEN = (28, 160, 108, 255)
BRIGHT_CYAN = (0, 255, 170, 255)
HOT_CYAN = (160, 255, 220, 255)

font_path = "C:/Windows/Fonts/consola.ttf"
try:
    font_tiny = ImageFont.truetype(font_path, 13)
    font_small = ImageFont.truetype(font_path, 16)
    font_mid = ImageFont.truetype(font_path, 22)
    font_large = ImageFont.truetype(font_path, 32)
    font_logo = ImageFont.truetype(font_path, 60)
except:
    font_tiny = font_small = font_mid = font_large = font_logo = ImageFont.load_default()

# -------------------------------------------------------------
# 1. INSTITUTE OPERATOR AVATAR (360 x 380)
# Matches reference: A friendly Soviet computer operator / engineer in lab coat
# -------------------------------------------------------------
def create_operator_avatar():
    w, h = 360, 380
    img = Image.new("RGBA", (w, h), BG)
    draw = ImageDraw.Draw(img)

    # Outer border with corner notches
    draw.rectangle([4, 4, w - 5, h - 5], outline=MID_GREEN, width=2)
    draw.rectangle([8, 8, w - 9, h - 9], outline=DIM_GREEN, width=1)

    # Corner brackets (registration marks)
    corner_len = 18
    for (bx, by, dx, dy) in [(4, 4, 1, 1), (w - 5, 4, -1, 1), (4, h - 5, 1, -1), (w - 5, h - 5, -1, -1)]:
        draw.line([(bx, by), (bx + dx * corner_len, by)], fill=BRIGHT_CYAN, width=3)
        draw.line([(bx, by), (bx, by + dy * corner_len)], fill=BRIGHT_CYAN, width=3)

    # Top header text
    draw.rectangle([10, 10, w - 10, 38], fill=DARK_TINT)
    draw.text((20, 14), "НИИЭВМ // ОПЕРАТОР ВЦ", font=font_small, fill=BRIGHT_CYAN)

    # Portrait frame (20, 46) to (w - 20, h - 54)
    px0, py0, px1, py1 = 20, 46, w - 20, h - 54
    draw.rectangle([px0, py0, px1, py1], outline=MID_GREEN, width=1)

    # Background grid / scanlines inside photo frame
    for y in range(py0 + 4, py1, 8):
        draw.line([(px0 + 1, y), (px1 - 1, y)], fill=(5, 38, 26, 255), width=1)
    for x in range(px0 + 20, px1, 30):
        draw.line([(x, py0 + 1), (x, py1 - 1)], fill=(5, 38, 26, 255), width=1)

    cx = (px0 + px1) // 2
    # Hair / Head of the engineer
    hair_top = py0 + 40
    draw.ellipse([cx - 48, hair_top, cx + 48, hair_top + 90], fill=MID_GREEN)
    # Forehead & face
    face_top = hair_top + 25
    draw.ellipse([cx - 38, face_top, cx + 38, face_top + 75], fill=BRIGHT_CYAN)
    # Hair part on top
    draw.chord([cx - 46, hair_top, cx + 46, hair_top + 45], 180, 360, fill=DIM_GREEN)

    # Round retro Soviet glasses (like scientific engineer)
    glass_y = face_top + 28
    draw.ellipse([cx - 30, glass_y, cx - 6, glass_y + 24], outline=DARK_TINT, fill=HOT_CYAN, width=2)
    draw.ellipse([cx + 6, glass_y, cx + 30, glass_y + 24], outline=DARK_TINT, fill=HOT_CYAN, width=2)
    draw.line([(cx - 6, glass_y + 12), (cx + 6, glass_y + 12)], fill=DARK_TINT, width=2)
    # Eyes behind glasses
    draw.rectangle([cx - 20, glass_y + 8, cx - 14, glass_y + 14], fill=DARK_TINT)
    draw.rectangle([cx + 14, glass_y + 8, cx + 20, glass_y + 14], fill=DARK_TINT)
    # Glasses highlight
    draw.line([(cx - 26, glass_y + 4), (cx - 18, glass_y + 12)], fill=BG, width=2)
    draw.line([(cx + 10, glass_y + 4), (cx + 18, glass_y + 12)], fill=BG, width=2)

    # Friendly smile & nose
    draw.line([(cx, glass_y + 18), (cx - 3, glass_y + 30), (cx + 2, glass_y + 30)], fill=DIM_GREEN, width=2)
    draw.arc([cx - 16, glass_y + 30, cx + 16, glass_y + 45], 20, 160, fill=DARK_TINT, width=2)

    # White Laboratory Coat (Халат инженера)
    neck_y = face_top + 72
    draw.rectangle([cx - 18, neck_y, cx + 18, neck_y + 20], fill=BRIGHT_CYAN)
    # Shirt collar & tie
    draw.polygon([(cx - 12, neck_y), (cx + 12, neck_y), (cx, neck_y + 18)], fill=DIM_GREEN)
    draw.polygon([(cx - 5, neck_y + 14), (cx + 5, neck_y + 14), (cx + 7, neck_y + 40), (cx - 7, neck_y + 40)], fill=DARK_TINT)

    # White lab coat shoulders & lapels
    draw.polygon([(cx - 120, py1), (cx - 65, neck_y + 10), (cx + 65, neck_y + 10), (cx + 120, py1)], fill=HOT_CYAN)
    # Coat lapels
    draw.polygon([(cx - 60, neck_y + 10), (cx - 20, neck_y + 55), (cx - 45, neck_y + 55)], fill=BRIGHT_CYAN)
    draw.polygon([(cx + 60, neck_y + 10), (cx + 20, neck_y + 55), (cx + 45, neck_y + 55)], fill=BRIGHT_CYAN)
    draw.line([(cx, neck_y + 40), (cx, py1)], fill=MID_GREEN, width=2)

    # Pocket with pens
    pocket_x = cx + 35
    pocket_y = neck_y + 50
    draw.rectangle([pocket_x, pocket_y, pocket_x + 35, pocket_y + 40], outline=MID_GREEN, fill=BRIGHT_CYAN, width=1)
    # Two pens sticking out
    draw.line([(pocket_x + 10, pocket_y - 10), (pocket_x + 10, pocket_y)], fill=DARK_TINT, width=3)
    draw.line([(pocket_x + 22, pocket_y - 8), (pocket_x + 22, pocket_y)], fill=DIM_GREEN, width=3)

    # Badge on coat: "ТАБЕЛЬ 104"
    draw.rectangle([cx - 75, pocket_y + 5, cx - 25, pocket_y + 35], fill=DARK_TINT, outline=MID_GREEN, width=1)
    draw.text((cx - 70, pocket_y + 10), "№ 104", font=font_tiny, fill=BRIGHT_CYAN)

    # Bottom status bar
    draw.rectangle([10, h - 46, w - 10, h - 10], fill=DARK_TINT)
    draw.text((20, h - 38), "ДОСТУП: ДЕЖУРНЫЙ ИНЖЕНЕР", font=font_small, fill=HOT_CYAN)

    img.save("Assets/Textures/CRT/SpyDossierAvatar.png")
    print("Operator avatar saved!")

# -------------------------------------------------------------
# 2. MAINFRAME SERVER COMPLEX ILLUSTRATION (300 x 380)
# Matches reference: Tall right panel with computing machine illustration
# -------------------------------------------------------------
def create_mainframe_schematic():
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
    draw.text((18, 14), "КОМПЛЕКС: ЕС ЭВМ-5061", font=font_small, fill=BRIGHT_CYAN)

    # Frame area
    sx0, sy0, sx1, sy1 = 18, 46, w - 18, h - 54
    draw.rectangle([sx0, sy0, sx1, sy1], outline=MID_GREEN, width=1)

    # Background grid
    for y in range(sy0 + 10, sy1, 20):
        draw.line([(sx0 + 1, y), (sx1 - 1, y)], fill=(5, 38, 26, 255), width=1)
    for x in range(sx0 + 10, sx1, 20):
        draw.line([(x, sy0 + 1), (x, sy1 - 1)], fill=(5, 38, 26, 255), width=1)

    # Draw authentic Soviet Server Cabinet illustration
    rack_x0, rack_y0, rack_x1, rack_y1 = sx0 + 20, sy0 + 15, sx1 - 20, sy1 - 20
    draw.rectangle([rack_x0, rack_y0, rack_x1, rack_y1], outline=BRIGHT_CYAN, width=2, fill=(8, 45, 32, 255))

    # Rack Title plate
    draw.rectangle([rack_x0 + 10, rack_y0 + 8, rack_x1 - 10, rack_y0 + 26], fill=DARK_TINT, outline=MID_GREEN, width=1)
    draw.text((rack_x0 + 25, rack_y0 + 10), "ЭВМ «ВЕКТОР-1»", font=font_tiny, fill=HOT_CYAN)

    # Upper Bay: Magnetic Tape Drives (Два бобинных накопителя)
    tape_y = rack_y0 + 36
    draw.rectangle([rack_x0 + 10, tape_y, rack_x1 - 10, tape_y + 80], outline=MID_GREEN, fill=DARK_TINT, width=1)
    # Left spool
    draw.ellipse([rack_x0 + 22, tape_y + 12, rack_x0 + 78, tape_y + 68], outline=BRIGHT_CYAN, width=2)
    draw.ellipse([rack_x0 + 38, tape_y + 28, rack_x0 + 62, tape_y + 52], fill=HOT_CYAN)
    # Right spool
    draw.ellipse([rack_x1 - 78, tape_y + 12, rack_x1 - 22, tape_y + 68], outline=BRIGHT_CYAN, width=2)
    draw.ellipse([rack_x1 - 62, tape_y + 28, rack_x1 - 38, tape_y + 52], fill=HOT_CYAN)
    # Tape threading path
    draw.line([(rack_x0 + 50, tape_y + 68), (rack_x0 + 90, tape_y + 74), (rack_x1 - 90, tape_y + 74), (rack_x1 - 50, tape_y + 68)], fill=HOT_CYAN, width=2)

    # Middle Bay: Register LED Matrix & Switches (Индикаторный регистр)
    led_y = tape_y + 90
    draw.rectangle([rack_x0 + 10, led_y, rack_x1 - 10, led_y + 50], outline=MID_GREEN, fill=DARK_TINT, width=1)
    draw.text((rack_x0 + 16, led_y + 4), "РЕГИСТР СОСТОЯНИЯ:", font=font_tiny, fill=DIM_GREEN)
    # 8 Register LEDs
    led_start_x = rack_x0 + 18
    for i in range(8):
        lx = led_start_x + i * 23
        col = HOT_CYAN if (i % 2 == 0 or i == 5) else DIM_GREEN
        draw.ellipse([lx, led_y + 22, lx + 14, led_y + 36], fill=col, outline=BRIGHT_CYAN, width=1)

    # Lower Bay: Removable Disk Cartridge Pack (Съемный пакет дисков ЕС-5061)
    disk_y = led_y + 60
    draw.rectangle([rack_x0 + 10, disk_y, rack_x1 - 10, disk_y + 46], outline=BRIGHT_CYAN, fill=DARK_TINT, width=2)
    draw.rectangle([rack_x0 + 25, disk_y + 10, rack_x1 - 25, disk_y + 36], outline=HOT_CYAN, fill=MID_GREEN, width=1)
    draw.text((rack_x0 + 35, disk_y + 15), "ДИСКОВОД ЕС-5061", font=font_tiny, fill=HOT_CYAN)

    # Bottom ventilation louvers
    vent_y = disk_y + 52
    for vy in range(vent_y, rack_y1 - 10, 6):
        draw.line([(rack_x0 + 20, vy), (rack_x1 - 20, vy)], fill=DIM_GREEN, width=1)

    # Footer status
    draw.rectangle([10, h - 46, w - 10, h - 10], fill=DARK_TINT)
    draw.text((18, h - 38), "МОДУЛЬ ВЦ // ОНЛАЙН", font=font_small, fill=HOT_CYAN)

    img.save("Assets/Textures/CRT/SpyBaseRadarSchematic.png")
    print("Mainframe schematic saved!")

# -------------------------------------------------------------
# 3. VECTOR SYSTEM LOGO (560 x 380)
# Matches reference: Top word, big horizontal arrow, bottom word
# Official Operating System boot splash: ВЕКТОР <=======> СИСТЕМА
# -------------------------------------------------------------
def create_system_logo():
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

    # Sub-header: Official OS classification
    draw.text((w // 2 - 150, 24), "/// ОПЕРАЦИОННАЯ СИСТЕМА ЭВМ ///", font=font_small, fill=MID_GREEN)

    # Top Hero Word: В Е К Т О Р
    top_y = 65
    title_text = "ВЕКТОР"
    bbox = draw.textbbox((0, 0), title_text, font=font_logo)
    tw = bbox[2] - bbox[0]
    tx = (w - tw) // 2

    # Phosphor glow behind text
    for offset in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
        draw.text((tx + offset[0], top_y + offset[1]), title_text, font=font_logo, fill=DIM_GREEN)
    draw.text((tx, top_y), title_text, font=font_logo, fill=HOT_CYAN)

    # Underline under top word
    draw.line([(tx - 15, top_y + 75), (tx + tw + 15, top_y + 75)], fill=MID_GREEN, width=2)

    # ---------------------------------------------------------
    # CENTER HERO ELEMENT: THE GIANT BIDIRECTIONAL ARROW
    # Matches reference image: <===================>
    # ---------------------------------------------------------
    arrow_y = h // 2
    arrow_left = 35
    arrow_right = w - 35
    arrow_thickness = 18
    head_size = 38

    rail_top = arrow_y - arrow_thickness // 2
    rail_bot = arrow_y + arrow_thickness // 2

    # Fill center shaft
    draw.rectangle([arrow_left + head_size, rail_top, arrow_right - head_size, rail_bot], fill=MID_GREEN)
    draw.rectangle([arrow_left + head_size + 4, rail_top + 4, arrow_right - head_size - 4, rail_bot - 4], fill=HOT_CYAN)

    # Left Arrowhead polygon
    draw.polygon([(arrow_left, arrow_y), (arrow_left + head_size, arrow_y - head_size), (arrow_left + head_size, arrow_y + head_size)], fill=BRIGHT_CYAN)
    draw.polygon([(arrow_left + 6, arrow_y), (arrow_left + head_size - 4, arrow_y - head_size + 10), (arrow_left + head_size - 4, arrow_y + head_size - 10)], fill=HOT_CYAN)

    # Right Arrowhead polygon
    draw.polygon([(arrow_right, arrow_y), (arrow_right - head_size, arrow_y - head_size), (arrow_right - head_size, arrow_y + head_size)], fill=BRIGHT_CYAN)
    draw.polygon([(arrow_right - 6, arrow_y), (arrow_right - head_size + 4, arrow_y - head_size + 10), (arrow_right - head_size + 4, arrow_y + head_size - 10)], fill=HOT_CYAN)

    # Decorative tech ticks across the arrow shaft
    for x in range(arrow_left + head_size + 15, arrow_right - head_size - 10, 25):
        draw.line([(x, rail_top - 4), (x, rail_bot + 4)], fill=BG, width=3)
        draw.line([(x, rail_top - 3), (x, rail_bot + 3)], fill=HOT_CYAN, width=1)

    # Arrow caption in center: "ШИНА ДАННЫХ"
    badge_w = 200
    draw.rectangle([w // 2 - badge_w // 2, arrow_y - 12, w // 2 + badge_w // 2, arrow_y + 12], fill=BG, outline=BRIGHT_CYAN, width=2)
    draw.text((w // 2 - 82, arrow_y - 8), "ШИНА ДАННЫХ", font=font_small, fill=HOT_CYAN)

    # ---------------------------------------------------------
    # Bottom Hero Word: С И С Т Е М А
    # ---------------------------------------------------------
    bot_y = arrow_y + 45
    bot_text = "СИСТЕМА"
    bbox_bot = draw.textbbox((0, 0), bot_text, font=font_logo)
    btw = bbox_bot[2] - bbox_bot[0]
    btx = (w - btw) // 2

    for offset in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
        draw.text((btx + offset[0], bot_y + offset[1]), bot_text, font=font_logo, fill=DIM_GREEN)
    draw.text((btx, bot_y), bot_text, font=font_logo, fill=HOT_CYAN)

    # Overline above bottom word
    draw.line([(btx - 15, bot_y - 8), (btx + btw + 15, bot_y - 8)], fill=MID_GREEN, width=2)

    # Sub-footer: Protocol info
    draw.text((w // 2 - 165, h - 35), "ВЕРСИЯ 3.2 // РЕДАКЦИЯ 1982 ГОДА", font=font_small, fill=MID_GREEN)

    img.save("Assets/Textures/CRT/DecoderArrowLogo.png")
    print("System logo saved!")

create_operator_avatar()
create_mainframe_schematic()
create_system_logo()
