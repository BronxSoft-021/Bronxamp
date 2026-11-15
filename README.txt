BRONXSOFT BRONXAMP

Muzički plejer za Windows u stilu Winampa/AIMP-a, ali čist i lep, sa emisijama za radio i (od v2) skinovima.

Pokretanje: prečica "BronxSoft Bronxamp" na desktopu (publish\Bronxamp.exe).
Muzika se dodaje dugmadima "Dodaj album" i "Dodaj pesme" ili prevlačenjem iz Explorer-a. Čitanje ide u pozadini i plejer se koristi za to vreme.
Podaci: %APPDATA%\BronxSoft Bronxamp\ (biblioteka, emisije, istorija, omoti, greške).
Tasteri: razmak = pusti/pauza, Ctrl+F = pretraga, Esc u pretrazi = briše upit, Delete = briše iz reda ili emisije.

Šta je u ovom folderu:
- CLAUDE.md                uputstvo za Claude Code (C#, WPF, BASS; šta radi v1, a šta v2 sa skinovima)
- Bronxamp.csproj, Zvuk/, Podaci/, Vm/, Prikaz/, Sistem/   kod programa
- Testovi/                 provere (dotnet run --project Testovi)
- lib/x64/                 BASS DLL-ovi i BASSenc (besplatni za nekomercijalnu upotrebu) + lame.exe za MP3 na etru (LGPL)
- assets/                  ikonica (app.ico) i logoi (logo_bronxamp, logo_bronxsoft, uvod)
- Napravi publish.bat, Napravi precicu.bat
- dizajn/                  izgled glavnog prozora i mini trake + koncepti skinova
- skins/magnetofon/        spisak delova za prvi skin i opisi za generator slika
- fonts/                   LCD fontovi DSEG (besplatni, licenca OFL)

Kako dalje:
1. Raspakuj, npr. u ~/undergrand-aplikacije/plejer
2. Otvori folder u Claude Code
3. Pošalji poruku iz START.txt
4. Za skin: napravi delove po skins/magnetofon/README.md i ubaci ih u taj folder

Verzije:
Cela istorija je u programu: klik na verziju levo dole (Verzije.cs).
- 0.87.73 beta  U etru kao klasičan plejer: Ctrl+A, Delete, desni klik „Obriši izabrano/sve“ i dugme „Obriši sve“
- 0.86.73 beta  dugme „Isprazni dan“ briše sve iz dana i ostavlja prazan spisak da ga sastaviš iznova
- 0.85.73 beta  Red i Ostatak dana su samo tih prelaz iz crne u sivu (bez linija i sužavanja)
- 0.85.72 beta  Ostatak dana je isti 3D usek (dubina i tamo gde pesme idu) i skroluje se kad stavki ima mnogo
- 0.84.71 beta  red je kutija u perspektivi: pod se sužava u dubinu, vide se gornja ivica i oba boka, pesme tonu u mrak
- 0.83.70 beta  gornja traka je jedna kartica (sat, etar, sledi), red je ležište sa slotovima, mirnija paleta, zaobljenije pločice
- 0.80.69 beta  red u koji idu pesme je udubljen (blaga dubina); ivica se oboji dok prevlačiš muziku u prozor
- 0.79.68 beta  polje za vreme pored svakog foldera i fajla (Metronom...), trajanje foldera i bloka
- 0.76.66 beta  jednostavniji Sastavi dan: semafor, tri koraka, dan se slaže sam, vreme iz imena foldera, dan po satima
- 0.71.64 beta  u ponoć se emitovanje samo prekine (poslednja pesma se završi), na serveru nastavlja njegov Auto DJ
- 0.70.64 beta  Pusti sa praznim redom pušta spisak na ekranu (od izabrane pesme ili od prve)
- 0.70.63 beta  spektar po hercima (20 Hz - 20 kHz, 31 stubić sa oznakama); naslov po sredini trake, veća vremena levo i desno
- 0.69.62 beta  ako server ne primi naslov preko BASSenc-a, šalje se sam na /admin/metadata i odgovor ide u dnevnik
- 0.69.61 beta  posle ponovnog povezivanja naziv i izvođač se ponovo pošalju; naslov i izvođač u dva reda u traci
- 0.69.58 beta  naslov po sredini, spektar kao ekvilajzer (32 stubića sa vrhovima), vremena u uglovima spektra
- 0.69.56 beta  traka „U etru“ kao RadioBOSS: spektar preko cele širine, vremena levo/desno, tačke u pozadini
- 0.68.55 beta  strim više nije usporen (enkoder dobija frekvenciju kartice, ne fiksno 44,1 kHz)
- 0.68.53 beta  kad muzika stane emitovanje čeka (nije „veza pukla“), naziv se šalje jednom, razlog u dnevniku
- 0.68.50 beta  kolona „#“, uvezene pesme po folderu i broju iz imena (ne po abecedi), „U etru“
- 0.67.48 beta  emitovanje čeka muziku (ne otvara vezu dok ništa ne svira), razlog greške u dnevniku
- 0.67.46 beta  horizontalni merač nivoa u donjoj traci: zeleno do −3 dB, blagi prelaz u crveno do 0 dB
- 0.66.46 beta  traka za vezu nije rastegnuta preko celog prozora, kraći tekstovi stanja
- 0.66.45 beta  donji deo: jedna traka za vezu sa serverom (tačka, status, dugme), boja prati stanje
- 0.66.44 beta  veza se sama vrati na server, jasnije stanje emitovanja (Povezan na server, Veza pukla)
- 0.65.43 beta  emitovanje na Zeno.fm: dugme Uključi emitovanje, status u traci, Probno i Test veze, naziv pesme na etru
- 0.63.42 beta  kraći levi meni, pilule iznad Biblioteke i Statistike, Emisije imaju svoju stranu
- 0.62.41 beta  podaci za Zeno.fm u Podešavanjima, Ostatak dana i Kraj plejliste, razmak u jinglovima
- 0.61.38 beta  raspored kao u RadioBOSS-u: gore sat, Na etru i Sledi, dole pesma, dugmad i status, bez mini trake
- 0.58.36 beta  dan ide bez prelaza između pesama
- 0.58.35 beta  bez dodatnih jinglova, blok posle hita kreće odmah
- 0.58.34 beta  pesme se pomeraju unutar bloka da ne štrče, blok kreće do 30 sekundi od vremena
- 0.58.32 beta  program ne meša pesme, prevlačenje i desni klik u listi dana, izmene dok dan traje
- 0.54.29 beta  vreme bloka i emisije je okvirno, poslednje pesme smeju posle ponoći, jinglovi druge boje
- 0.54.26 beta  Počni iznova, izbor početka dana se više ne gubi, bez kvačice Bez prekida
- 0.53.23 beta  nov gornji deo, Na etru, Ostatak dana, Folderi, Emitovano, desni klik na traku
- 0.46.20 beta  plejliste sa vremenom kao blokovi sa jinglovima i hitom
- 0.43.19 beta  početak dana bira korisnik, jingl na svake tri pesme bez ponavljanja
- 0.42.17 beta  blok hita kreće 15 sekundi pre termina
- 0.42.16 beta  Sastavi dan: slaganje i puštanje dana po satu, jinglovi, dnevnik
- 0.37.16 beta  izvođač i naslov odvojeni, kolone se pomeraju prevlačenjem
- 0.36.14 beta  zbijen spisak pesama, izbor kolona, uklanjanje izabranih pesama
- 0.34.11 beta  dugmad Pomoć (F1) i O programu, spisak tehnologija i Licence.txt
- 0.32.11 beta  logoi bez pozadine, natpisi na dugmadima za nasumično i ponavljanje, nasumičan početak albuma
- 0.32.8 beta  logo u gornjoj traci kad ništa ne svira
- 0.32.7 beta  logoi Bronxamp i BronxSoft, uvodni ekran, nova ikonica
- 0.30.6 beta  naziv se piše BronxSoft, sa velikim S
- 0.30.5 beta  Dodaj album, Dodaj pesme i prevlačenje iz Explorer-a, dodata muzika ide u red
- 0.26.5 beta  vidljiva dugmad na albumima i pesmama, uklanjanje albuma iz biblioteke, ispravka nestajanja omota
- 0.22.4 beta  novi naziv BronxSoft Bronxamp, Šta je novo, upravljanje redom, emisije bez unapred upisanih, ispravke (Sačuvaj u Podešavanjima, pauza)
- 0.18.0 beta  sve funkcije specifikacije: reprodukcija bez prekida i crossfade, biblioteka, zid omota, pretraga, emisije sa KRAJ EMISIJE, statistika, preslušavanje, mini traka, Windows integracija
- 0.1.0 alfa  specifikacija i dizajn (bez koda)
