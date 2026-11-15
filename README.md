# BronxSoft Bronxamp

> **English:** Bronxamp is a free music player and radio automation program for Windows, made for UnderGRAND,
> a non-profit internet radio from Novi Sad, Serbia. It builds a whole broadcast day from folders (hit of the day
> on the hour, jingles every three songs, shows at their time), plays it by the clock and streams it to an
> Icecast server. The program itself is in Serbian only. Status: **beta**.

Besplatan muzički plejer i program za puštanje radija, napravljen za **UnderGRAND** radio iz Novog Sada kao
zamena za RadioBOSS. Pouzdanost ide ispred izgleda: program ne sme da utihne.

**Status: beta.** Program se svakodnevno razvija i još nije u etru.

## Šta radi

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

## Preuzimanje

Gotov program je na strani [Releases](../../releases): raspakuj zip i pokreni `Bronxamp.exe`.
Ne treba ništa da se instalira (.NET je uz program).

- Windows 10 ili 11, 64-bitni.
- Program nije digitalno potpisan, pa Windows SmartScreen pri prvom pokretanju može da pita
  („Više informacija“ → „Svejedno pokreni“).
- Podaci programa (biblioteka, podešavanja, složeni dani, dnevnik) su u `%APPDATA%\BronxSoft Bronxamp\`.
- Lozinka za emitovanje se čuva šifrovana i može da je pročita samo isti Windows nalog na istom računaru.

## Pravljenje iz koda

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

## Licenca

Kod Bronxampa je pod licencom **MIT** (vidi [LICENSE](LICENSE)).

Delovi drugih autora zadržavaju svoje licence; spisak i puni tekstovi su u [Licence.txt](Licence.txt).
Najvažnije:

- **BASS, BASSmix, BASSFLAC, BASSOPUS i BASSenc** (Un4seen Developments) su besplatni samo za
  **nekomercijalnu** upotrebu. Bronxamp se ne prodaje i od njega se ne zarađuje. Ko želi da koristi kod u
  komercijalne svrhe, mora da kupi licencu na [un4seen.com](https://www.un4seen.com).
- **LAME** (`lame.exe`) i **TagLib#** su pod LGPL 2.1; to su zasebni fajlovi i mogu se zameniti drugom verzijom.
- **DSEG** fontovi su pod SIL Open Font License 1.1.
- ManagedBass, CommunityToolkit.Mvvm i Microsoft.Data.Sqlite su pod MIT, a SQLite je javno vlasništvo.

## Autor

**BronxSoft**, za UnderGRAND radio, Novi Sad. Program je besplatan i nema reklama.
