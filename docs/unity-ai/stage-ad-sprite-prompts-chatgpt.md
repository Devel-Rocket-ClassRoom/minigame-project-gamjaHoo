# Stage AD — 스프라이트 생성 프롬프트 (ChatGPT / gpt-image-1)

> ChatGPT 이미지 생성용. 각 코드블록을 **통째로 복사**해 ChatGPT 에 보내세요.
> **한 메시지에 하나씩** 생성하는 게 품질이 가장 좋습니다 (한 번에 여러 개 = 디테일 뭉개짐).
>
> **ChatGPT 팁**
> - 생성된 이미지를 **PNG 로 다운로드** → 아래 파일명 정확히 (공백 포함) → 폴더에 투입.
> - 투명 배경이 안 나오면 "make the background fully transparent (PNG alpha), no checkerboard" 라고 한 번 더 요청.
> - 글자/테두리 텍스트가 섞이면 "remove all text and letters, keep it clean" 으로 재생성.
> - 색은 정확히 안 맞아도 OK (플레이스홀더 대체용).

---

## (선택) 먼저 보낼 스타일 고정 메시지

> 새 대화 시작 시 이걸 먼저 보내두면 이후 20개가 톤이 일관됩니다. (생략 가능 — 각 프롬프트가 자체 완결형)

```
I need 20 football club crest icons in ONE consistent style. For every image: a flat vector circular emblem — a single solid-colored disc that fills the square frame, centered on a fully transparent background (PNG with alpha). Inside the disc is one bold, simple, geometric motif. Flat colors only: no gradients, no shadows, no 3D, no shading, and absolutely NO text, letters, numbers, or words anywhere. Keep all 20 in the exact same minimalist flat style. I'll give you them one at a time. Square 1:1, high resolution, transparent background.
```

---

## 1. 구단 크레스트 20종

> 저장 위치: `Assets/_Project/Data/Resources/ClubCrests/` · 파일명 정확히 일치 (공백 포함).

### → 저장: `Skyblues.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid sky-blue disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a white sailing ship with three sails facing right. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Ravens.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid deep-charcoal disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a stylized white raven head in profile. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Cannons.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid crimson-red disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a golden cannon pointing right. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Red Devils.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid red disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a black devil silhouette holding a trident. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Blues.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid royal-blue disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a white lion rampant holding a staff. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Cockerels.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid navy disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a white rooster (cockerel) standing on a football. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Magpies.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A disc fills the square frame, split into black and white halves, centered on a fully transparent background (PNG alpha). Inside the disc: a perched magpie bird with black-and-white plumage. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Lions.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid claret (dark wine red) disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a golden lion rampant. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Seagulls.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid sky-blue disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a white seagull in flight with wings spread. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Hammers.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid claret (dark wine red) disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: two crossed golden rivet hammers. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Eagles.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid royal-blue disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a red eagle with spread wings. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Cottagers.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A white disc with a black ring fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a simple black riverside cottage house silhouette. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Bees.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A disc fills the square frame with red and white vertical stripes, centered on a fully transparent background (PNG alpha). Inside the disc: a yellow-and-black bumblebee. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Toffees.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid royal-blue disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a white tall clock tower. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Foxes.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid royal-blue disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a white fox head facing forward. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Foresters.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid red disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a green leafy oak tree. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Cherries.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A disc fills the square frame with red and black vertical stripes, centered on a fully transparent background (PNG alpha). Inside the disc: a pair of red cherries with a green stem. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Clarets.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A single solid claret (dark wine red) disc fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a white stork standing with a long beak. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Black Cats.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A disc fills the square frame, split into red and white halves, centered on a fully transparent background (PNG alpha). Inside the disc: a black sitting cat silhouette. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

### → 저장: `Peacocks.png`

```
Flat vector circular football-club crest emblem, modern minimalist style. A white disc with blue and gold accents fills the square frame, centered on a fully transparent background (PNG alpha). Inside the disc: a peacock with fanned tail feathers. Bold simple geometric shapes, flat colors, no gradients, no shadows, no 3D, and absolutely no text or letters. Square 1:1, high resolution.
```

---

## 2. 시설 아이콘 8종

> 흰 단색 글리프, 투명 배경. 저장 위치: `Assets/_Project/Data/Resources/FacilityIcons/` · 파일명 = FacilityType enum 명 정확히.

### → 저장: `Scout.png`

```
Flat minimalist white pictogram icon of a magnifying glass over a small person silhouette. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

### → 저장: `Training.png`

```
Flat minimalist white pictogram icon of a football next to a training cone. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

### → 저장: `YouthCoach.png`

```
Flat minimalist white pictogram icon of a clipboard with a whistle. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

### → 저장: `YouthRecruitment.png`

```
Flat minimalist white pictogram icon of a person silhouette with a plus sign. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

### → 저장: `YouthFacility.png`

```
Flat minimalist white pictogram icon of an academy building with a small star above it. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

### → 저장: `Medical.png`

```
Flat minimalist white pictogram icon of a medical cross inside a rounded square. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

### → 저장: `Stadium.png`

```
Flat minimalist white pictogram icon of a stadium arena silhouette with floodlights. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

### → 저장: `Gym.png`

```
Flat minimalist white pictogram icon of a dumbbell. A single solid white shape, centered on a fully transparent background (PNG alpha). Bold, simple, clean, no color, no gradients, no shadows, and absolutely no text or letters. Square 1:1.
```

---

## 3. 생성 후 (Unity)

1. 각 PNG 를 위 파일명 정확히 (공백 포함) 로 저장:
   - 구단: `Assets/_Project/Data/Resources/ClubCrests/` (예: `Red Devils.png`, `Black Cats.png`)
   - 시설: `Assets/_Project/Data/Resources/FacilityIcons/` (예: `Scout.png`)
2. 임포트 설정 (PNG 선택 → Inspector):
   - **Texture Type**: `Sprite (2D and UI)`
   - **Sprite Mode**: `Single`
   - **Alpha Is Transparency**: ON
3. 같은 파일명이면 기존 플레이스홀더를 **자동 대체** (CrestProvider 가 파일명을 키로 로드).

## 4. EPL 모티프 대응 (참고)

Skyblues=Man City(배) / Cannons=Arsenal(대포) / Red Devils=Man Utd / Blues=Chelsea(사자) /
Cockerels=Spurs(수탉) / Magpies=Newcastle / Seagulls=Brighton / Hammers=West Ham /
Eagles=Crystal Palace / Cottagers=Fulham / Bees=Brentford / Toffees=Everton(타워) /
Foxes=Leicester / Foresters=Nottm Forest / Cherries=Bournemouth / Clarets=Burnley /
Black Cats=Sunderland / Peacocks=Leeds / Lions·Ravens=일반.
