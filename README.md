# BronxSoft Bronxamp

**[Srpski](#srpski) · [English](#english)**

---

## Srpski

Besplatan muzički plejer i program za puštanje radija, napravljen za **UnderGRAND** radio iz Novog Sada kao
zamena za RadioBOSS. Pouzdanost ide ispred izgleda: program ne sme da utihne.

**Status: beta.** Program se svakodnevno razvija i još nije u etru. Sav tekst u programu je na srpskom.

### Šta radi

**Program za etar**
- **Sastavi dan:** ubaciš pesme u folder sa šemom dana, kažeš koji je hit dana i u koje vreme idu emisije, a
  program sam ubaci jinglove (na svake tri pesme, nijedan se ne ponavlja u toku dana), hit dana na svaka dva
  sata i složi ceo dan sa tačnim vremenom svake stavke.
- Semafor pokazuje da li je sve spremno, a dan po satima šta ide kad i koliko traje.
- Vreme može da stoji u imenu foldera („20.00 Koncertacija“), pa ne mora da se kuca.
- Dan ide po satu: hit dana kreće na nekoliko sekundi od punog sata, blokovi i emisije do pola minuta od svog
  vremena. Posle pada ili restarta program nastavlja od stavke koja po satu treba da svira.
- **Emitovanje** na Icecast server (na primer Zeno.fm) preko BASSenc i LAME, sa naslovom pesme. Veza se sama
  vraća kad pukne i sama se prekida u ponoć.
- Dnevnik emitovanog: šta je stvarno pušteno i kad.

**Plejer**
- Biblioteka sa skeniranjem foldera (MP3, FLAC, OGG, OPUS, M4A, WAV), zid omota, spisak pesama i pretraga
  jednim poljem („ekv 1983“, „metal 90-e“).
- Reprodukcija bez prekida između pesama i crossfade.
- Emisije sa istorijom i upozorenjem na skoro puštene pesme, statistika.
- Preslušavanje na slušalicama dok glavni izlaz svira.
- Spektar po hercima (20 Hz - 20 kHz), merač nivoa, medijski tasteri i kontrole Windowsa.

### Preuzimanje

Gotov program je na strani [Releases](../../releases): raspakuj zip i pokreni `Bronxamp.exe`.
Ne treba ništa da se instalira (.NET je uz program).

- Windows 10 ili 11, 64-bitni.
- Program nije digitalno potpisan, pa Windows SmartScreen pri prvom pokretanju može da pita
  („Više informacija“ → „Svejedno pokreni“).
- Podaci programa (biblioteka, podešavanja, složeni dani, dnevnik) su u `%APPDATA%\BronxSoft Bronxamp\`.
- Lozinka za emitovanje se čuva šifrovana i može da je pročita samo isti Windows nalog na istom računaru.

### Pravljenje iz koda

Treba [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bat
dotnet build Bronxamp.csproj
dotnet run --project Testovi
```

`Napravi publish.bat` pravi `publish\Bronxamp.exe` sa svim što mu treba. Biblioteke BASS (x64) su u `lib\x64`,
preuzete sa [un4seen.com](https://www.un4seen.com).

Kod je podeljen na `Zvuk/` (BASS, mikser, emitovanje), `Podaci/` (biblioteka, slaganje dana, podešavanja),
`Vm/` (view-modeli), `Prikaz/` (WPF prozori) i `Sistem/` (integracija sa Windowsom). U `Testovi/` su provere
zvuka, dana, biblioteke i emitovanja, kao i ekranski test koji otvara pravi prozor.

### Licenca

Kod Bronxampa je pod licencom **MIT** (vidi [LICENSE](LICENSE)).

Delovi drugih autora zadržavaju svoje licence; spisak i puni tekstovi su u [Licence.txt](Licence.txt).
Najvažnije:

- **BASS, BASSmix, BASSFLAC, BASSOPUS i BASSenc** (Un4seen Developments) su besplatni samo za
  **nekomercijalnu** upotrebu. Bronxamp se ne prodaje i od njega se ne zarađuje. Ko želi da koristi kod u
  komercijalne svrhe, mora da kupi licencu na [un4seen.com](https://www.un4seen.com).
- **LAME** (`lame.exe`) i **TagLib#** su pod LGPL 2.1; to su zasebni fajlovi i mogu se zameniti drugom verzijom.
- **DSEG** fontovi su pod SIL Open Font License 1.1.
- ManagedBass, CommunityToolkit.Mvvm i Microsoft.Data.Sqlite su pod MIT, a SQLite je javno vlasništvo.

### Autor

**BronxSoft**, za UnderGRAND radio, Novi Sad. Program je besplatan i nema reklama.

---

## English

A free music player and radio automation program for Windows, made for **UnderGRAND**, a non-profit internet
radio from Novi Sad, Serbia, as a replacement for RadioBOSS. Reliability comes before looks: the program must
never go silent.

**Status: beta.** It is developed every day and is not on air yet. The program itself is in Serbian only.

### Features

**On-air automation**
- **Build the day** ("Sastavi dan"): put songs into the day's schedule folder, say which song is the hit of the
  day and when the shows start, and the program places the jingles (every three songs, none repeated during the
  day) and the hit of the day every two hours, and builds the whole day with an exact time for every item.
- A traffic light shows whether everything is ready, and the day-by-hours view shows what plays when and for
  how long.
- A time can be part of a folder name ("20.00 Koncertacija"), so nobody has to type it.
- The day follows the clock: the hit of the day starts within a few seconds of the hour, blocks and shows
  within half a minute of their time. After a crash or restart the program carries on from the item that
  should be playing by the clock.
- **Streaming** to an Icecast server (for example Zeno.fm) through BASSenc and LAME, with the song title. The
  connection comes back by itself when it drops, and closes by itself at midnight.
- A log of what was really played and when.

**Player**
- Library with folder scanning (MP3, FLAC, OGG, OPUS, M4A, WAV), cover wall, song list and one-field search
  ("ekv 1983", "metal 90-e").
- Gapless playback and crossfade.
- Shows with history and a warning for recently played songs, statistics.
- Pre-listening on headphones while the main output plays.
- Spectrum by frequency (20 Hz - 20 kHz), level meter, media keys and Windows media controls.

### Download

The ready program is on the [Releases](../../releases) page: unzip it and run `Bronxamp.exe`.
Nothing needs to be installed (.NET comes with the program).

- Windows 10 or 11, 64-bit.
- The program is not code-signed, so Windows SmartScreen may ask on the first run
  ("More info" → "Run anyway").
- Program data (library, settings, built days, log) is in `%APPDATA%\BronxSoft Bronxamp\`.
- The streaming password is stored encrypted and can only be read by the same Windows account on the same
  computer.

### Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bat
dotnet build Bronxamp.csproj
dotnet run --project Testovi
```

`Napravi publish.bat` builds `publish\Bronxamp.exe` with everything it needs. The BASS libraries (x64) are in
`lib\x64`, downloaded from [un4seen.com](https://www.un4seen.com).

The code is split into `Zvuk/` (sound: BASS, mixer, streaming), `Podaci/` (data: library, building the day,
settings), `Vm/` (view models), `Prikaz/` (WPF windows) and `Sistem/` (Windows integration). `Testovi/` holds
tests for sound, the day, the library and streaming, and a screen test that opens the real window. Names in the
code are in Serbian.

### License

Bronxamp's own code is licensed under **MIT** (see [LICENSE](LICENSE)).

Parts by other authors keep their own licenses; the list and full texts are in [Licence.txt](Licence.txt).
Most importantly:

- **BASS, BASSmix, BASSFLAC, BASSOPUS and BASSenc** (Un4seen Developments) are free for **non-commercial**
  use only. Bronxamp is not sold and earns no money. Anyone who wants to use the code commercially must buy a
  license at [un4seen.com](https://www.un4seen.com).
- **LAME** (`lame.exe`) and **TagLib#** are under LGPL 2.1; they are separate files and can be replaced with
  another version.
- The **DSEG** fonts are under the SIL Open Font License 1.1.
- ManagedBass, CommunityToolkit.Mvvm and Microsoft.Data.Sqlite are under MIT, and SQLite is public domain.

### Author

**BronxSoft**, for UnderGRAND radio, Novi Sad. The program is free and has no ads.
