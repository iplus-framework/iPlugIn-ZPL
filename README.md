# iPlugIn-ZPL

ZPL plugin supports Scryber templates through the custom layout renderer path.

## Scryber barcode support

Yes, barcode rendering is supported for Scryber reports.

Implementation details:
- The renderer detects barcode metadata on rendered components.
- GS1 payload generation reuses the same GS1 parser/model logic used by WPF FlowDoc (`VBShowColumns` + `VBShowColumnsKeys`).
- Output uses ZPL-native commands (`^BQN` for QR and `^BC` for CODE128/GS1 CODE128).

Supported metadata keys:
- `data-barcode-type` or `data-zpl-barcode-type`: `QRCODE`, `CODE128`.
- `data-barcode-value`: explicit barcode value (optional).
- `data-vb-content`: source object path (for GS1), e.g. `CurrentFacilityCharge`.
- `data-vb-show-columns`: comma-separated value paths.
- `data-vb-show-columns-keys`: comma-separated GS1 AI keys.
- `data-barcode-height` (for CODE128/QR scale fallback).
- `data-barcode-x` (optional X position override).

## FlowDoc to Scryber conversion (your example)

Original FlowDoc had:
- label text (`Chargennummer`, `Split`)
- lot number value
- QR barcode with GS1 fields from `CurrentFacilityCharge`

Equivalent Scryber template:

```html
<!doctype html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
	<meta charset="utf-8" />
	<style>
		@page { size: 420pt 700pt; margin: 20pt; }
		body { font-family: Calibri, Arial, sans-serif; font-size: 12pt; margin: 0; }
		.line { margin: 0 0 6pt 0; }
		.lot { font-size: 14pt; font-weight: bold; }
		.barcode-center { text-align: center; margin-top: 10pt; }
	</style>
</head>
<body>
	<p class="line">Chargennummer:</p>
	<p class="line lot">{{vb.Get("CurrentFacilityCharge/FacilityLot/LotNo")}}</p>

	<p class="line">Split:</p>

	<p class="barcode-center"
		 data-barcode-type="QRCODE"
		 data-barcode-height="6"
		 data-vb-content="CurrentFacilityCharge"
		 data-vb-show-columns="FacilityLot/LotNo,FacilityLot/ProductionDate,FacilityLot/ExpirationDate,Material/MaterialNo,SplitNo,FBCTargetQuantityUOM"
		 data-vb-show-columns-keys="10,11,17,240,30,310d">
		{{vb.Get("CurrentFacilityCharge/FacilityLot/LotNo")}}
	</p>
</body>
</html>
```

Notes:
- For GS1 barcodes, the renderer builds GS1 payload from `data-vb-show-columns` + `data-vb-show-columns-keys` and `data-vb-content`.
- The paragraph body text can stay as a readable fallback value; GS1 payload takes precedence for barcode generation.



# Zebra Mobile Printers - Black Mark Alignment Fix

This repository addresses a common issue when driving mobile Zebra printers (such as the **ZQ630 Plus**) via custom drivers (e.g., C# printing routines using raw ZPL).

## The Problem
When sending configuration parameters and print payloads as separate ZPL payloads, desktop and industrial Zebra printers (ZT/ZD series) handle them seamlessly. However, mobile Zebra printers will often:
1. Skip a blank label before every print job.
2. Offset the print coordinates by up to 50mm.
3. Lock up the physical **Feed** button into an error state.

This happens because the mobile printer's hybrid OS interprets multiple `^XA ... ^XZ` blocks in rapid succession as entirely new print jobs, triggering an unneeded media re-calibration cycle. Furthermore, standard ZPL `^LL` (Label Length) parameters are frequently bypassed if the underlying CPCL environment isn't locked down first.

## The Solution
To guarantee precise black mark detection and prevent blank labels from skipping on mobile printers, you must:
1. **Prepend SGD (SetGetDo) commands** directly to the top of the payload.
2. **Merge the configuration and the print layout** into a single, unified data transmission block containing only **one** `^XA` and `^XZ` wrapper.

### Optimized Template Payload

Send the following example payload as a single string/byte array to the printer:

Configuration section
```zcl
! U1 setvar "media.type" "label"
! U1 setvar "media.sense_mode" "bar"
! U1 setvar "zpl.label_length" "{labelHeight}"
~SD10
~TA000
~JSB
```
Content section
```zcl
^XA
^SZ2
^PW812
^LL1218
^PON
^PR5,5
^PMN
^MNM
^LS0
^MMT,N
^LH0,0
^CI28
^A0N,25,0
^FO50,10
^FH^FDTest^FS
^XZ
```
```

### Breakdown of Key Commands used for Mobile Adjustments

* **`! U1 setvar "media.type" "label"`**: Tells the mobile environment to treat the media as defined labels rather than continuous receipt paper.
* **`! U1 setvar "media.sense_mode" "bar"`**: Explicitly forces the reflective sensor to look for black marks.
* **`! U1 setvar "zpl.label_length" "1218"`**: Defines the exact physical length (152.4 mm @ 203 DPI = 1218 dots) at the hardware level *before* ZPL parsing starts. 
* **`^MNM`**: The ZPL equivalent configuration for Media Tracking: Mark (Black Mark).
* **Removal of `^MTD` and `^JUS`**: Removed to prevent the printer from re-initializing its printhead settings and constantly writing to flash memory during active printing cycles.

