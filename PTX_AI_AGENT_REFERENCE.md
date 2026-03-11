# PTX Pattern Exchange Format - AI Agent Reference

Derived from:
- `PTX_FORMAT.txt` from `Pattern Exchange File - Specification v1.21` (Magi-Cut, dated March 14, 2025)
- `PTX_Agent_Spec.md`

This document is intended for AI agents, parsers, importers, exporters, viewers, and automation systems that need a practical description of the PTX file format and how it works operationally.

---

# 1. What PTX is

PTX (Pattern Exchange) is a neutral exchange format for:

- cutting lists
- board stock lists
- material definitions
- optimization patterns
- saw cutting instructions
- label synchronization data
- offcut data
- optional drawing vectors

It is used between:

- CAD/design systems
- cut-list generators
- optimization software
- panel saw controllers
- manufacturing systems

The data structure exists in two representations:

1. ASCII comma-separated text file (`.PTX`)
2. Microsoft Access database (`.MDB`)

Most modern integrations use the ASCII CSV form.

---

# 2. Format-level rules

## 2.1 File structure

PTX is record-based. Each line starts with a record type token:

HEADER,...
JOBS,...
PARTS_REQ,...

Each record type corresponds to a logical table in the MDB variant.

## 2.2 Naming rules

The original spec uses uppercase names with underscores. Table and field names are limited to 10 characters.

## 2.3 CSV rules

All normal CSV rules apply:

- comma-delimited fields
- quotes may be used around text
- text containing commas must be quoted
- leading spaces are ignored
- trailing commas are optional

## 2.4 Filename notes

The ASCII file suffix is `.PTX`. The basename may be a job number, order number, or batch name. Some controllers impose filename restrictions, for example accepting only 5 digits for a job number.

## 2.5 Data typing rules from the spec

The summary section defines these field categories:

- `DIM`: dimension; metric range typically `0.0..9999.9`, decimal-inch range `0.000..999.9`
- `FLT`: floating-point number
- `IDX`: integer index used for linking records
- `INT`: integer
- `QTY`: quantity integer, maximum `99999`
- `TXT`: text

Additional import constraints from the spec:

- material codes may not contain spaces; spaces can be converted to `_` on import
- material, part, and board codes may be converted to uppercase on import

## 2.6 Index rules

All index numbers are integer values starting at 1 and incrementing consecutively. In particular:

- `JOB_INDEX` must be unique across jobs
- `PART_INDEX` must be unique within a job
- `BRD_INDEX` must be unique within a job
- `PTN_INDEX` must be unique within a job
- `CUT_INDEX` must restart at 1 for each pattern and increment consecutively

All part, board, pattern, and cut records must carry the appropriate `JOB_INDEX`.

---

# 3. Record types

The v1.21 spec describes 12 main record types in the exchange structure, plus optional `VECTORS` drawing geometry:

Pre-optimization:

- `HEADER`
- `JOBS`
- `PARTS_REQ`
- `PARTS_INF`
- `PARTS_UDI`
- `PARTS_DST`
- `BOARDS`
- `MATERIALS`
- `NOTES`

Post-optimization:

- `OFFCUTS`
- `PATTERNS`
- `PTN_UDI`
- `CUTS`
- `VECTORS`

`VECTORS` is optional drawing data and is sometimes treated as an auxiliary record set rather than core production data.

---

# 4. Core relationships

Key link fields:

- `JOB_INDEX`
- `PART_INDEX`
- `BRD_INDEX`
- `MAT_INDEX`
- `PTN_INDEX`
- `CUT_INDEX`
- `OFC_INDEX`

Main relationships:

- `PARTS_REQ.MAT_INDEX -> MATERIALS.MAT_INDEX`
- `PARTS_INF.(JOB_INDEX, PART_INDEX) -> PARTS_REQ`
- `PARTS_UDI.(JOB_INDEX, PART_INDEX) -> PARTS_REQ`
- `PARTS_DST.(JOB_INDEX, PART_INDEX) -> PARTS_REQ`
- `PATTERNS.BRD_INDEX -> BOARDS.BRD_INDEX`
- `CUTS.PTN_INDEX -> PATTERNS.PTN_INDEX`
- `CUTS.PART_INDEX -> PARTS_REQ.PART_INDEX`
- `CUTS.PART_INDEX -> X + OFFCUTS.OFC_INDEX`
- `PTN_UDI.(JOB_INDEX, PTN_INDEX, BRD_INDEX, STRIP_INDEX) -> strip-level matching data for a pattern`
- `VECTORS.(JOB_INDEX, PTN_INDEX, CUT_INDEX) -> CUTS`

If `JOBS` records are absent, parts and patterns are assumed to belong to the same job.

---

# 5. Record layouts and meanings

## 5.1 HEADER

Structure:

`HEADER, VERSION, TITLE, UNITS, ORIGIN, TRIM_TYPE`

Fields:

- `VERSION`: file version, for example `1.08` or `1.21`
- `TITLE`: file title
- `UNITS`: `0 = metric`, `1 = decimal inches`
- `ORIGIN`: vector drawing origin
- `TRIM_TYPE`: whether waste/fixed trim is cut first or last

`ORIGIN` values apply to `VECTORS`; the origin for `CUTS` is assumed to be top-left:

- `0 = top to bottom, left to right`
- `1 = top to bottom, right to left`
- `2 = bottom to top, left to right`
- `3 = bottom to top, right to left`

`TRIM_TYPE`:

- `0 = trim waste piece first`
- `1 = trim fixed trim first`

## 5.2 JOBS

Structure:

`JOBS, JOB_INDEX, NAME, DESC, ORD_DATE, CUT_DATE, CUSTOMER, STATUS, OPT_PARAM, SAW_PARAM, CUT_TIME, WASTE_PCNT`

Important fields:

- `STATUS`: `0 = not optimised`, `1 = optimised`, `2 = optimise failed`
- `OPT_PARAM`: optimizer parameter filename
- `SAW_PARAM`: saw parameter filename
- `CUT_TIME`: total cutting time in seconds
- `WASTE_PCNT`: overall waste percentage by board area

Dates are specified as `DD/MM/YYYY`.

## 5.3 PARTS_REQ

Structure:

`PARTS_REQ, JOB_INDEX, PART_INDEX, CODE, MAT_INDEX, LENGTH, WIDTH, QTY_REQ, QTY_OVER, QTY_UNDER, GRAIN, QTY_PROD, UNDER_PROD_ERROR, UNDER_PROD_ALLOWED, UNDER_PROD_PLUSPART`

Fields beyond the common subset matter:

- `QTY_OVER`: allowed over-production
- `QTY_UNDER`: allowed under-production
- `QTY_PROD`: quantity produced by patterns
- `UNDER_PROD_ERROR`: not produced because of error
- `UNDER_PROD_ALLOWED`: not produced because under-production was allowed
- `UNDER_PROD_PLUSPART`: plus-parts not produced

Grain:

- `0 = no grain / part can be rotated`
- `1 = grain along board length / part cannot be rotated`
- `2 = grain along board width / part must be rotated`

Important practical rule:

- For layout reconstruction from optimized data, `CUTS` are authoritative for physical orientation.
- For synthetic reconstruction from `PARTS_REQ` only, transpose `LENGTH/WIDTH` when `GRAIN = 2`.

## 5.4 PARTS_INF

Structure:

`PARTS_INF, JOB_INDEX, PART_INDEX, DESC, LABEL_QTY, FIN_LENGTH, FIN_WIDTH, ORDER, EDGE1, EDGE2, EDGE3, EDGE4, EDG_PG1, EDG_PG2, EDG_PG3, EDG_PG4, FACE_LAM, BACK_LAM, CORE, DRAWING, PRODUCT, PROD_INFO, PROD_WIDTH, PROD_HGT, PROD_DEPTH, PROD_NUM, ROOM, BARCODE1, BARCODE2, COLOUR, SECOND_CUT_LENGTH, SECOND_CUT_WIDTH`

Operational meaning:

- label-printing and product metadata
- edgeband codes and edge program codes
- finished dimensions after edging/trimming
- CNC/drawing filename
- product and room assignment
- barcode payloads
- second-cut dimensions

Notable details:

- `LABEL_QTY = 0` means no labels for this part
- default label quantity is 1 if not specified
- `EDGE1/EDGE2` are bottom/top length edges
- `EDGE3/EDGE4` are left/right width edges

## 5.5 PARTS_UDI

Structure:

`PARTS_UDI, JOB_INDEX, PART_INDEX, INFO1, INFO2, ... INFO60`

This is free-form per-part metadata. The official spec allows up to 60 `INFO` fields.

Typical uses:

- label text
- customer metadata
- cabinet metadata
- routing/CNC metadata
- arbitrary downstream integration fields

Agents should treat this record as flexible and implementation-specific.

## 5.6 PARTS_DST

Structure:

`PARTS_DST, JOB_INDEX, PART_INDEX, PART_LAY_L, PART_LAY_W, PART_LAY_O, STK_HGHT_Q, STK_HGHT_D, STATION, QTY_STACKS, BTM_TYPE, BTM_DESC, BTM_MATL, BTM_LENGTH, BTM_WIDTH, BTM_THICK, OVER_LEN, OVER_WID, BTM_LAY_L, BTM_LAY_W, TOP_TYPE, TOP_DESC, TOP_MATL, TOP_LENGTH, TOP_WIDTH, TOP_THICK, TOP_LAY_L, TOP_LAY_W, SUP_TYPE, SUP_DESC, SUP_MATL, SUP_LENGTH, SUP_WIDTH, SUP_THICK, SUP_LAY_L, SUP_LAY_W, STATION2`

This record is for destacking / pallet / station handling.

Key semantics:

- `PART_LAY_L`: parts per stack in length
- `PART_LAY_W`: parts per stack in width
- `PART_LAY_O`: `1 = part is lengthways`, `0 = part is widthways`
- `STK_HGHT_Q`: stack height by quantity
- `STK_HGHT_D`: stack height by dimension
- `QTY_STACKS`: number of stacks/pallets
- `OVER_LEN`, `OVER_WID`: overhang per side
- bottom/top/support fields describe pallet boards, covers, and supports

## 5.7 BOARDS

Structure:

`BOARDS, JOB_INDEX, BRD_INDEX, CODE, MAT_INDEX, LENGTH, WIDTH, QTY_STOCK, QTY_USED, COST, STK_FLAG, INFORMATION, MAT_PARAM, GRAIN, TYPE, BIN, SUPPLIER, EXTRA_INFORMATION, COST_METHOD`

Key fields:

- `QTY_STOCK`: available sheets; default `99999`, `0 = none`
- `QTY_USED`: sheets used by patterns
- `COST`: cost per area or per sheet depending on `COST_METHOD`
- `STK_FLAG`: flag controlling insufficient-stock handling
- `MAT_PARAM`: material parameter filename
- `TYPE`:
  - `0 = stock board`
  - `1 = offcut`
  - `2 = automatic offcut`
- `GRAIN`:
  - `0 = none`
  - `1 = along board length`
  - `2 = along board width`
- `COST_METHOD`:
  - `0 = cost per unit area`
  - `1 = cost per sheet`

## 5.8 MATERIALS

Structure:

`MATERIALS, JOB_INDEX, MAT_INDEX, CODE, DESC, THICK, BOOK, KERF_RIP, KERF_XCT, TRIM_FRIP, TRIM_VRIP, TRIM_FXCT, TRIM_VXCT, TRIM_HEAD, TRIM_FRCT, TRIM_VRCT, RULE1, RULE2, RULE3, RULE4, MAT_PARAM, GRAIN, PICTURE, DENSITY`

Important physical parameters:

- `BOOK`: max sheets per book / cutting height
- `KERF_RIP`: rip saw kerf
- `KERF_XCT`: crosscut kerf
- `TRIM_FRIP`: fixed rip trim, includes kerf
- `TRIM_VRIP`: minimum waste rip trim, includes kerf
- `TRIM_FXCT`: fixed crosscut trim, includes kerf
- `TRIM_VXCT`: minimum waste crosscut trim, includes kerf
- `TRIM_HEAD`: internal head-cut trim, includes kerf
- `TRIM_FRCT`: fixed recut trim, includes kerf
- `TRIM_VRCT`: minimum waste recut trim, includes kerf

Optimization rules:

- `RULE1`: nesting/recut limit, `1..9`
- `RULE2`: head cuts allowed, `0/1`
- `RULE3`: board rotation allowed (short rip), `0/1`
- `RULE4`: show separate patterns for duplicate parts, `0/1`

Other fields:

- `PICTURE`: either a solid color like `RGB(255:0:0)` or an image filename like `Teak.bmp`
- `DENSITY`: metric tons per m3 or pounds per ft3 depending on unit mode

Important trim assumption from the spec:

- for rip, cross, and recut trims, one trim is constant and the other includes the waste strip
- either the leading edge is trimmed and waste comes last, or waste is removed first and the last cut is fixed trim

## 5.9 NOTES

Structure:

`NOTES, JOB_INDEX, NOTES_INDEX, TEXT`

Optional textual notes for job-level operator or integration information.

## 5.10 OFFCUTS

Structure:

`OFFCUTS, JOB_INDEX, OFC_INDEX, CODE, MAT_INDEX, LENGTH, WIDTH, OFC_QTY, GRAIN, COST, TYPE, EXTRA_INFORMATION, COST_METHOD`

Key fields:

- `OFC_INDEX`: unique offcut index used by `CUTS`
- `OFC_QTY`: quantity of this offcut size
- `GRAIN`:
  - `0 = none`
  - `1 = along offcut length`
  - `2 = along offcut width`
- `TYPE`:
  - `1 = offcut`
  - `2 = automatic offcut`
- `COST_METHOD`:
  - `0 = cost per unit area`
  - `1 = cost per offcut`

## 5.11 PATTERNS

Structure:

`PATTERNS, JOB_INDEX, PTN_INDEX, BRD_INDEX, TYPE, QTY_RUN, QTY_CYCLES, MAX_BOOK, PICTURE, CYCLE_TIME, TOTAL_TIME, PATTERN_PROCESSING`

This record describes one optimized pattern.

Important fields:

- `TYPE`: determines first-cut direction and pattern kind
- `QTY_RUN`: sheets cut to this pattern
- `QTY_CYCLES`: number of cycles/books
- `MAX_BOOK`: max sheets per book for the pattern
- `PICTURE`: pattern picture filename
- `CYCLE_TIME`: time to cut one cycle
- `TOTAL_TIME`: total pattern time

`TYPE` values from the spec:

Fixed patterns:

- `0 = rip length first, non-head-cut pattern`
- `1 = turn board before ripping, non-head-cut pattern`
- `2 = head-cut pattern, head cut across width`
- `3 = head-cut pattern, head cut along length`
- `4 = crosscut only`

Template patterns:

- `5 = create master part, divide at saw`
- `6 = create master part, divide at machining centre`
- `7 = cut parts in main pattern`
- `8 = cut parts in separate pattern`

Practical note:

- In viewer/reconstruction logic, this `TYPE` field can change the axis used by the first cut and therefore the orientation of the derived layout.

## 5.12 PTN_UDI

Structure:

`PTN_UDI, JOB_INDEX, PTN_INDEX, BRD_INDEX, STRIP_INDEX, INFO1, INFO2, ... INFO99`

Purpose:

- stores strip-level matching information
- used when all parts in a strip must share the same matching data

Key fields:

- `STRIP_INDEX`: strip number, top-to-bottom then left-to-right
- `INFO1..INFO99`: matching fields for the strip

This record is pattern-level metadata, not general geometry.

## 5.13 CUTS

Structure:

`CUTS, JOB_INDEX, PTN_INDEX, CUT_INDEX, SEQUENCE, FUNCTION, DIMENSION, QTY_RPT, PART_INDEX, QTY_PARTS, COMMENT`

This is the authoritative machine-instruction record set.

Field meanings:

- `CUT_INDEX`: sequential within a pattern
- `SEQUENCE`: saw processing order
- `FUNCTION`:
  - `0 = head cut`
  - `1 = rip cut`
  - `2 = cross cut`
  - `3 = 3rd phase / recut`
  - `4 = 4th phase / recut`
  - `5..9 = higher recut phases`
  - `90..93 = trim/waste cut corresponding to cut phase`
- `DIMENSION`: cut size in measurement units
- `QTY_RPT`: repeat count
- `PART_INDEX`: `0` if no part; otherwise part index or `X + OFC_INDEX`
- `QTY_PARTS`: total quantity of this part produced by this cut across all cycles
- `COMMENT`: optional narrative

Critical operational behavior:

- `CUTS` are not necessarily listed in machine execution order; later phases are nested
- `SEQUENCE` is optional; if absent, the saw or post-processor determines order
- sequence numbers advance by repeat count
- the origin for `CUTS` is assumed to be top-left

Special cases:

- duplicate labeled parts are represented by dummy `CUTS` rows with `DIMENSION = 0`, `QTY_RPT = 0`, nonzero `PART_INDEX`
- exact-fit patterns may produce two parts from one final cut; the last part record can have `QTY_RPT = 0` while keeping a nonzero dimension
- waste gaps are represented by `PART_INDEX = 0`; the specified dimension is the falling piece, while actual gap = falling piece + kerf x 2
- offcuts are referenced as `Xn`, where `n` is `OFC_INDEX`
- the spec includes examples of multiple strips being cut together and repeated cuts sharing the same sequence band

## 5.14 VECTORS

Structure:

`VECTORS, JOB_INDEX, PTN_INDEX, CUT_INDEX, X_START, Y_START, X_END, Y_END`

Purpose:

- optional geometric description of the pattern as vectors
- linked back to `CUTS` via `CUT_INDEX`

Important geometry rules:

- all vector coordinates are absolute
- the drawing origin comes from `HEADER.ORIGIN`
- positions include saw kerf offset from origin
- unlike `CUTS`, which are relative machine instructions, `VECTORS` are absolute drawing geometry

This distinction matters:

- `CUTS`: relative saw process instructions
- `VECTORS`: absolute drawing lines

---

# 6. How PTX works operationally

## 6.1 Typical workflow

1. `HEADER`, `JOBS`, `PARTS_*`, `BOARDS`, and `MATERIALS` define demand and stock.
2. The optimizer generates `PATTERNS`, `CUTS`, `OFFCUTS`, optionally `PTN_UDI` and `VECTORS`.
3. The saw/controller uses `CUTS` for machine execution and label synchronization.
4. Optional vector/picture data is used for visualization, not core saw logic.

## 6.2 What is authoritative

For physical saw behavior:

- `CUTS` are authoritative

For part requested quantities and metadata:

- `PARTS_REQ` plus optional `PARTS_INF`, `PARTS_UDI`, `PARTS_DST`

For material/trims/kerf:

- `MATERIALS`

For stock board identity:

- `BOARDS`

For drawing geometry:

- `VECTORS` if present

## 6.3 Relative vs absolute geometry

This is a common source of bugs:

- `CUTS` dimensions are relative cut sizes and machine instructions
- `VECTORS` coordinates are absolute geometry

Agents reconstructing a layout should prefer:

1. `VECTORS` if exact drawing geometry is needed and present
2. otherwise `CUTS` + `PATTERNS.TYPE` + `BOARDS` + `MATERIALS`

## 6.4 Pattern orientation and first-cut direction

`PATTERNS.TYPE` changes how the first cut is interpreted:

- some patterns start with a rip-length-first mode
- some require board rotation before ripping
- some start with head cuts
- some are crosscut-only

Any layout reconstruction that assumes all patterns start on the same axis is incomplete.

## 6.5 Label synchronization

`CUTS.QTY_PARTS` is aggregated across all cycles/books. The saw is expected to distribute labels across books internally, using `SEQUENCE`, repeat counts, and book counts from the pattern/material context.

---

# 7. Practical parsing rules for agents

Agents should:

- parse PTX as CSV, not naive comma-split text if quoted fields are possible
- treat each record type as a relational table
- allow optional records and optional trailing fields
- tolerate absent `JOBS`
- tolerate absent `PARTS_INF`, `PARTS_UDI`, `PARTS_DST`, `NOTES`, `PTN_UDI`, `VECTORS`
- preserve `COMMENT` fields but not depend on them for semantics
- resolve `PART_INDEX = Xn` to `OFFCUTS.OFC_INDEX = n`
- treat unknown user-defined fields as opaque metadata

Recommended reconstruction order:

1. parse `HEADER`
2. parse `MATERIALS`
3. parse `BOARDS`
4. parse `PARTS_REQ`
5. parse optional part metadata records
6. parse `PATTERNS`
7. parse `OFFCUTS`
8. parse `CUTS`
9. parse `VECTORS`

---

# 8. Key examples from the spec that matter

## 8.1 Waste gap semantics

A waste cut such as:

`CUTS,1,1,4,2,1,30.4,1,0,0`

does not mean the gap is 30.4 mm. It means the falling waste piece is 30.4 mm. If kerf is 4.8 mm, the total gap is:

`30.4 + 4.8 + 4.8 = 40.0`

## 8.2 Duplicate labels

A second part from the same physical cut may appear as:

`CUTS,...,0,2,0.0,0,2,1`

This is a label/duplicate record, not a new physical cut.

## 8.3 Offcut production

A cut may also produce an offcut:

`CUTS,...,0,2,0,0,X3,20`

This means the cut links to `OFFCUTS.OFC_INDEX = 3`.

## 8.4 Multiple strips cut together

The examples explicitly show repeated rip strips where later crosscuts share the same sequence group. Parsers and viewers must not assume one strip maps to one unique sequence block.

---

# 9. PTX to saw/controller mapping

Typical mapping called out by the spec and agent notes:

| PTX | Saw/controller meaning |
|---|---|
| `PATTERNS` | board/pattern header |
| `PARTS_REQ` | part demand / saw part definitions |
| `CUTS` | saw cut instructions |
| `OFFCUTS` | leftover/offcut boards |
| `MATERIALS` | material, thickness, trim, kerf |

The saw-side format is derived from PTX, but PTX itself is the exchange contract.

---

# 10. Guidance for generation

When generating PTX:

- keep indices consecutive and unique in scope
- keep `JOB_INDEX` consistent across linked records
- ensure `PARTS_REQ.MAT_INDEX` refers to a real material
- ensure `PATTERNS.BRD_INDEX` refers to a real board
- ensure every `CUTS.PTN_INDEX` refers to a real pattern
- use `PART_INDEX = 0` for waste-only cuts
- use `PART_INDEX = Xn` only when `OFFCUTS.OFC_INDEX = n` exists
- do not use zero-dimension part cuts unless encoding duplicate-label semantics
- use quotes around any CSV text containing commas

---

# 11. Bottom line for AI agents

PTX is a relational, record-based manufacturing exchange format. The most important implementation truths are:

- `CUTS` drive machine behavior
- `PATTERNS.TYPE` affects cut orientation and layout reconstruction
- `MATERIALS` defines kerf and trim semantics
- `OFFCUTS` are linked through `CUTS` using `X + OFC_INDEX`
- `VECTORS` are optional absolute geometry, not the same thing as `CUTS`
- `PARTS_INF`, `PARTS_UDI`, and `PARTS_DST` carry important downstream metadata even though they do not define core cut geometry

If an agent fully understands those relationships and the record layouts above, it can reliably parse, validate, generate, and troubleshoot PTX files.
