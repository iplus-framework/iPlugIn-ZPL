## iPlugIn-ZPL

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
