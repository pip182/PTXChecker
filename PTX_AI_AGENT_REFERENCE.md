
# PTX Pattern Exchange Format — AI Agent Reference (Enhanced)

This document is designed for **AI agents, parsers, and automation systems** that must read,
interpret, or generate PTX files. It restructures the official PTX specification into a
machine‑friendly schema with explicit relationships and operational semantics.

---

# 1. PTX Overview

PTX (Pattern Exchange Format) is used in woodworking and panel optimization workflows to exchange:

- Cut lists
- Material definitions
- Board stock definitions
- Optimization patterns
- Saw cutting instructions
- Offcut information
- Label synchronization data

PTX files are typically **ASCII CSV records** where each line begins with a **record type**.

Example:

HEADER,...
PARTS_REQ,...
CUTS,...

---

# 2. Record Types

PTX contains the following record types.

Pre‑optimization records

HEADER
JOBS
PARTS_REQ
PARTS_INF
PARTS_UDI
PARTS_DST
BOARDS
MATERIALS
NOTES

Post‑optimization records

OFFCUTS
PATTERNS
PTN_UDI
CUTS
VECTORS

---

# 3. Key Index Fields

These act like primary keys.

JOB_INDEX
PART_INDEX
BRD_INDEX
MAT_INDEX
PTN_INDEX
CUT_INDEX
OFC_INDEX

Relationships:

PARTS_REQ.MAT_INDEX → MATERIALS.MAT_INDEX
PATTERNS.BRD_INDEX → BOARDS.BRD_INDEX
CUTS.PART_INDEX → PARTS_REQ.PART_INDEX
CUTS.PART_INDEX → X + OFFCUTS.OFC_INDEX

---

# 3.1 PARTS_REQ and grain direction

PARTS_REQ fields typically include (order may vary by exporter): JOB_INDEX, PART_INDEX, Part name, Material/seq, Width, Height, Qty, …, **Grain** (often at field index 10).

**Grain flag (placement/orientation):**

| Value | Meaning |
|-------|--------|
| 0 | No grain — parts placed in same orientation as board |
| 1 | Grain along board **length** — same orientation as board |
| 2 | Grain along board **width** — square parts placed in **opposite** orientation (rotated 90° on board) |

When **grain = 2**, the part’s length (grain direction) aligns with the board width axis; the optimizer places the part accordingly and CUTS reflect the physical cut. When reconstructing layout from CUTS, use CUTS dimensions as authoritative. When generating synthetic CUTS from PARTS_REQ only (e.g. no CUTS in file), use part Width/Height swapped for grain = 2 (strip height = part Width, cross dimension = part Height).

---

# 4. CUTS Record — Cutting Instructions

The CUTS record defines **the physical cutting instructions executed by the saw**.

It determines:

- cutting order
- cut type
- parts produced
- label synchronization
- offcuts produced

## Structure

CUTS,
JOB_INDEX,
PTN_INDEX,
CUT_INDEX,
SEQUENCE,
FUNCTION,
DIMENSION,
QTY_RPT,
PART_INDEX,
QTY_PARTS,
COMMENT

### Field definitions

JOB_INDEX  
Links to job.

PTN_INDEX  
Links to pattern.

CUT_INDEX  
Sequential index for each pattern.

SEQUENCE  
Defines execution order of cuts.

FUNCTION  
Cut type.

DIMENSION  
Distance of cut from reference edge.

QTY_RPT  
Number of times the cut repeats.

PART_INDEX  
Identifies produced part.

QTY_PARTS  
Total parts produced across all cycles.

COMMENT  
Optional text.

---

# 5. CUT Function Codes

| Code | Meaning |
|-----|--------|
0 | head cut |
1 | rip cut |
2 | cross cut |
3 | 3rd phase / recut |
4 | 4th phase / recut |
5‑9 | higher recut phases |
90 | trim phase 0 |
91 | trim phase 1 |
92 | trim phase 2 |
93 | trim phase 3 |

---

# 6. Special CUTS Behavior

### Duplicate Parts

Multiple sheets in a book may produce parts with identical dimensions but unique labels.

To represent this:

Dummy CUTS records are created:

dimension = 0  
qty_rpt = 0  
part_index ≠ 0

These records represent **additional labels** rather than physical cuts.

---

### Exact Fit Patterns

When strips perfectly fill a board:

A crosscut may produce **two parts simultaneously**.

Representation:

last CUTS record

dimension = actual value  
qty_rpt = 0

This differentiates from dummy records.

---

### Sequence Behavior

Sequence indicates **logical stack order**.

Sequence numbers increment by repeat count.

Example

repeat = 3  
sequence = 4

Actual sequence numbers

4
5
6

Next sequence = 7

---

# 7. Waste Strip Modeling

Waste strips are defined using:

PART_INDEX = 0

The dimension equals **falling piece size**, not the total gap.

Actual gap size:

gap = falling_piece + kerf × 2

Example

kerf = 4.8 mm

CUTS,1,1,4,2,1,30.4,1,0,0

Waste gap:

30.4 + 4.8 + 4.8 = 40 mm

---

# 8. Offcut Linking

CUTS can generate offcuts.

PART_INDEX syntax:

X + OFC_INDEX

Example

PART_INDEX = X3

This refers to:

OFFCUTS,OFC_INDEX=3

---

# 9. Label Synchronization Logic

CUTS records enable the saw to synchronize label printing.

Example scenario:

pattern run quantity = 20  
book size = 6 sheets

Cycles:

6
6
6
2

CUTS record may show:

PART 1 quantity = 14
PART 2 quantity = 6

The saw internally distributes labels across cycles.

---

# 10. Example CUTS Records

Example from specification.

CUTS,1,1,1,1,1,500.0,1,0,0
CUTS,1,1,2,4,2,800.0,3,1,14
CUTS,1,1,3,0,2,0.0,0,2,1
CUTS,1,1,4,2,1,30.4,1,0,0
CUTS,1,1,5,3,1,200.0,1,0,0
CUTS,1,1,6,5,2,1400.0,1,8,5

Interpretation:

rip strip 500mm  
crosscut producing part 1  
crosscut producing part 2  
rip waste strip  
rip strip 200mm  
crosscut producing part 8

---

# 11. Pattern Reconstruction Strategy

To reconstruct the cutting pattern:

1. Load PATTERNS record
2. Load BOARDS board size
3. Process CUTS ordered by SEQUENCE
4. Apply kerf from MATERIALS
5. Track produced parts
6. Track offcuts

---

# 12. PTX → SAW Mapping

Typical mapping used by HOMAG saws.

| PTX | SAW |
|----|----|
PATTERNS | BRD2 |
PARTS_REQ | PNL2 |
CUTS | XBRD2 |
OFFCUTS | leftover board |
MATERIALS | board thickness/material |

The SAW file contains **optimized pattern geometry** derived from PTX CUTS.

---

# 13. Parsing Strategy (Pseudocode)

read file line by line

split by comma

record_type = first token

switch(record_type)

case HEADER
    parse_header()

case MATERIALS
    add_material()

case BOARDS
    add_board()

case PARTS_REQ
    add_part()

case PATTERNS
    add_pattern()

case CUTS
    add_cut_instruction()

case OFFCUTS
    add_offcut()

---

# 14. Agent Interpretation Rules

Agents should:

• treat records as relational tables  
• resolve links using index fields  
• allow optional fields  
• tolerate missing metadata records  
• ignore unknown user‑defined fields

CUTS records should be considered the **authoritative machine instructions**.

---

# 15. Example Minimal PTX File

HEADER,1.21,"Example Job",0,0,1

MATERIALS,1,1,WHITE18,"White board",18,4,4.8,4.8,10,10,8,8,8,8,8,4,1,1,1,WLAM18,0,RGB(255:255:255),0.9

BOARDS,1,1,WHITE18-2440x1220,1,2440,1220,20

PARTS_REQ,1,1,SIDE,1,720,560,2,0,0,1

PATTERNS,1,1,1,0,1,1,1,"Pattern1",30,30,0

CUTS,1,1,1,1,1,560,1,1,2
CUTS,1,1,2,2,2,720,1,1,2

