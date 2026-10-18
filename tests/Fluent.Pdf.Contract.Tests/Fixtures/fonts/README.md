# Original and licensed font fixtures

`original.ttf` and `original.otf` contain original geometric outlines encoded by FontTools 4.61.1, including a TrueType composite, CJK scalar and supplementary scalar. `original.cff` is the CFF program extracted from the original OpenType fixture. `original-seac.otf` independently encodes the same accented geometry with a legacy CFF composite. `original.ttc` holds two explicitly different faces. `generate-original.py` records the independent encoder calls and fixed timestamps.

`original-type1.t1` is original Type 1 assembly with an unused glyph and a `seac` dependency. The independent `t1asm` tool creates its PFA/PFB fixtures. No source code from a font/PDF implementation is copied.

`licensed-cjk.otf` is a small independently subsetted/renamed SIL OFL CJK face. `licensed-cjk-source.json` identifies the original collection/face/version/digest and retained scalars. `Noto-CJK-LICENSE.txt` retains the full upstream/distribution notice. No proprietary font is included. `manifest.json` records exact fixture digests. These small fixtures do not replace the required complete licensed font corpus or production qualification.
