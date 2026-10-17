# Original filter fixture

`lzw-earlychange-1.bin` is an LZW strip encoded independently with Pillow 12.3.0 and its TIFF encoder. The original 64 x 64 grayscale samples are defined by `sample[i] = (i * 37 + floor(i / 11)) mod 256`. SHA-256: `8d0a16c0951d8e5c5fe16d41f8d99393213ec359a4fa44840f9a9bf68df2c69f` (1,868 encoded bytes).

The deterministic input is original and distributed under the repository MIT license. The file contains encoded sample data, not third-party code or reference binaries. Tests decode to the explicit sample formula and independently compare the encoder's bytes, exercising code-width transitions with EarlyChange 1.
