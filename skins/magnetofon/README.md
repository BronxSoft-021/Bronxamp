# Skin "Magnetofon" — spisak delova

Uzor: `dizajn/koncepti_skinova/magnetofon_bronx.png` (gornji deo: magnetofon + VU-metri + "sada svira" + plejlista).
Osnovna veličina skina: **1280 × 720** (slike praviti u **2560 × 1440**, tj. 2×).
Sve delove snimiti u ovaj folder pod tačnim imenima. Kad delovi stignu, Claude Code pravi `skin.json` sa pozicijama.

## Delovi (ime fajla — veličina na 2× — providno? — šta je)

| Fajl | Veličina (px, 2×) | Providno | Opis |
|---|---|---|---|
| `pozadina.png` | 2560×1440 | da, oko ivica | Celo kućište: izgreben metal, rđa, šrafovi, ploče, nalepnica "UnderGRAND Radio", UG zvezda. BEZ kolutova, BEZ igala, BEZ teksta pesama, BEZ dugmića (na njihovim mestima prazna udubljenja). |
| `kolut.png` | 620×620 | da | Jedan kolut trake spreda, metalni, sa tri crvena otvora, centriran. Koristi se dva puta (levi i desni). |
| `traka_namotaj.png` | 620×620 | da | Samo namotana smeđa traka (pun krug), ide ispod koluta; program je skalira po napretku. |
| `vu_skala.png` | 560×300 | ne | Prazna VU skala (krem, brojevi -20 … +3, crvena zona), BEZ igle. Koristi se za L i D. |
| `vu_igla.png` | 20×260 | da | Samo crna igla VU-metra, vertikalna, osa okretanja na dnu. |
| `dugme_play_normal.png` / `_hover` / `_pressed` | 200×140 | da | Crveno metalno dugme, ikonica pauza/play crta program ili posebne slike ispod. |
| `dugme_pauza_*.png` | 200×140 | da | Isto, sa ikonom pauze. |
| `dugme_nazad_*.png`, `dugme_napred_*.png`, `dugme_shuffle_*.png`, `dugme_repeat_*.png` | 180×120 | da | Tamna metalna dugmad, normal/hover/pressed. |
| `knob_jacina.png` | 360×360 | da | Okruglo hromirano dugme za jačinu, pogled odozgo; crvena oznaka na vrhu. |
| `knob_skala.png` | 460×460 | da | Crveni prsten-skala oko dugmeta za jačinu (crtice). |
| `ekran_pozadina.png` | 1200×300 | ne | Tamni "displej" za sada svira (omot + naslov + spektar); program crta sadržaj. |
| `lista_pozadina.png` | 900×860 | ne | Tamni panel za plejlistu. |

Tekst (naslov, izvođač, vreme, plejlista), spektar i talasni oblik crta program.

## Opisi za generator slika (kopiraj i prilagodi)

Zajednički stil (dodaj na kraj svakog opisa):
> grungy industrial audio equipment, worn scratched dark metal, rust, red accents, warm tungsten lighting,
> photorealistic, front orthographic view, no perspective, no text, high detail, 4k

- **pozadina.png**: "Front view of a vintage reel-to-reel tape deck and amplifier panel combined into one wide music
  player body, empty round recesses where two tape reels go, two empty rectangular windows for VU meters, empty dark
  display window in the middle, empty dark panel on the right for a playlist, empty button slots at the bottom,
  bolts and screws, a worn paper sticker that says 'UnderGRAND Radio', a stencilled red star. Transparent background
  outside the device." (Natpis na nalepnici proveriti ili dopisati kasnije u Photopei.)
- **kolut.png**: "A single metal tape reel seen straight from the front, three red cut-out windows, centered,
  isolated on transparent background."
- **traka_namotaj.png**: "A full circle of wound brown magnetic tape seen from the front, isolated on transparent background."
- **vu_skala.png**: "An analog VU meter face, cream paper with black scale from -20 to +3, red zone above 0,
  letters 'VU', no needle, straight front view."
- **vu_igla.png**: "A single thin black VU meter needle, vertical, isolated on transparent background."
- **knob_jacina.png**: "A chrome volume knob seen from directly above, knurled edge, small red marker at the top,
  isolated on transparent background."
- **dugmad**: "A set of chunky worn metal push buttons for a tape deck: play, pause, rewind, fast-forward, shuffle,
  repeat, each in three states (normal, glowing on hover, pressed in), isolated on transparent background."

Ako generator ne daje providnu pozadinu: uklanjanje pozadine u Photopei (Magic Cut / Remove BG) ili remove.bg.
