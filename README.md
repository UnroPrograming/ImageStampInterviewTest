# ImageStamp — .NET backend technical test

`ImageStamp` is an HTTP service that composes images: it takes a base PNG and a list of layers,
draws them on top respecting position, opacity and stacking order, and returns the resulting PNG.
Metadata for every composition is stored in PostgreSQL.

It builds, it runs and its tests are green. There are no gaps to fill in and no `TODO`s to hunt for.

---

## 1. Your task

Two product requests have come in. Implement them.

### A. New layer type: `blur`

Blurs a region of the canvas.

```json
{ "type": "blur", "x": 0, "y": 120, "width": 600, "height": 180, "sigma": 8, "zIndex": 2 }
```

- It applies **at its `zIndex` position**: it blurs whatever is already painted underneath, and
  layers with a higher `zIndex` are drawn afterwards, sharp, on top.
- It only affects the rectangle given.
- It is persisted like every other layer.

> `GaussianBlur` already ships with the version of ImageSharp the project uses. You do not need any
> new packages.

### B. New endpoint: `POST /api/compositions/batch`

Applies **the same layers to several base images** in a single call. This is the real use case:
stamping the same logo and the same frame onto the batch of photos for one vehicle, which in
production is between 20 and 40 images from a dealership camera.

- `multipart/form-data`, with **several** files in the `baseImages` field.
- The `layers` field and the image-layer files are the same as today, and are shared across all the
  base images.
- Returns a **ZIP** with one PNG per base image.
- Each image produces its own composition row, exactly as if the existing endpoint had been called.

---

## 2. Ground rules

**Change whatever you need to.** If anything in the current code gets in your way — the modelling,
the contract, the data access, the libraries, the structure — change it. You are not expected to
preserve any decision made in this template.

**Keep the tests green** and persistence working.

**Tests for whatever you fix.** If something breaks along the way, the fix comes with a test that
fails before it and passes after.

---

## 3. What to submit

- The code and the tests.
- A **half-page `NOTES.md`**, three bullets:
  - What you added.
  - What you ran into along the way, and how you dealt with it.
  - What you spotted and chose not to touch.
- Run instructions, if you changed how the project starts.

Zip up the repository or share a Git repository with us.

**Time: 2-3 hours.** We do not want you to spend a full day on this. If you do not get to everything,
submit what you have and say so in `NOTES.md`: we would rather see one piece done well than two done
halfway.

In the interview we will talk through the code, the decisions you made and the alternatives you
ruled out.

---

## 4. Getting it running

You need [.NET SDK 10](https://dotnet.microsoft.com/download) (`dotnet --version` → `10.x`) and
Docker.

```bash
docker compose up --build          # postgres + the API on http://localhost:8080
curl http://localhost:8080/health  # {"status":"healthy"}
```

Or just the database, with the API from your IDE (it comes up on `http://localhost:5080`):

```bash
docker compose up -d postgres
dotnet run --project src/ImageStamp
```

> PostgreSQL is published on host port **15432**, not 5432, so it does not clash with a local
> install you may already have. Inside the Docker Compose network the API still connects to
> `postgres:5432`.

### Tests

```bash
dotnet test
```

The persistence tests only run if you point them at a database:

```bash
# bash
export IMAGESTAMP_TEST_POSTGRES="Host=localhost;Port=15432;Database=imagestamp;Username=imagestamp;Password=imagestamp"
dotnet test
```

```powershell
# PowerShell
$env:IMAGESTAMP_TEST_POSTGRES = "Host=localhost;Port=15432;Database=imagestamp;Username=imagestamp;Password=imagestamp"
dotnet test
```

---

## 5. The API today

### `POST /api/compositions`

Composes an image and returns the PNG. Request is `multipart/form-data`:

| Field | Type | Description |
| --- | --- | --- |
| `baseImage` | file | Background PNG. Defines the size of the result. |
| `layers` | text | JSON array describing the layers. |
| *(one per image layer)* | file | The PNG for each `image` layer, under the field name given in its `imageKey`. |

Response: `200 OK`, `Content-Type: image/png`. The `X-Composition-Id` header carries the identifier
of the stored composition.

Layers are drawn ordered by `zIndex`: **higher values end up on top**. When two layers share a
`zIndex`, they are drawn in the order they appear in the `layers` array. There are two types today:

```json
{ "type": "image", "x": 10,  "y": 10, "opacity": 1.0, "zIndex": 1, "imageKey": "logo" }
{ "type": "solid", "x": 100, "y": 50, "opacity": 0.5, "zIndex": 3, "width": 300, "height": 100, "color": "#FF0000" }
```

```bash
curl -X POST http://localhost:8080/api/compositions \
  -F "baseImage=@base.png" \
  -F "logo=@logo.png" \
  -F 'layers=[
        {"type":"image","x":10,"y":10,"opacity":1.0,"zIndex":1,"imageKey":"logo"},
        {"type":"solid","x":100,"y":50,"width":300,"height":100,"color":"#FF0000","opacity":0.5,"zIndex":3}
      ]' \
  --output result.png
```

### `GET /api/compositions` · `GET /api/compositions/{id}`

Lists the stored compositions with their layers, or returns a single one.

```bash
curl http://localhost:8080/api/compositions
curl http://localhost:8080/api/compositions/<id>
```

---

## 6. Layout

```text
src/ImageStamp/            Single project for the whole application
  Api/                     Controllers and HTTP contracts
  Core/                    Models, validation and the composition engine
  Infrastructure/          PostgreSQL access
  Program.cs               Composition root and the /health endpoint
tests/ImageStamp.Tests/    Current tests
docker/postgres/init.sql   Initial schema
```

Data access uses `Npgsql` and explicit SQL. Image binaries are **not** stored in PostgreSQL: only
the metadata for each composition.
