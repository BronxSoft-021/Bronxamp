using System.Reflection;

namespace Bronxamp;

public sealed record Verzija(string Faza, string Datum, string[] Nove, string[] Dorade)
{
    /// <summary>Counted from the history, never written by hand. See <see cref="Verzije"/>.</summary>
    public string Broj { get; internal set; } = "";

    public IEnumerable<string> Izmene => Nove.Concat(Dorade);

    public IEnumerable<string> Vrste
    {
        get
        {
            if (Nove.Length > 0) yield return "Novo";
            if (Dorade.Length > 0) yield return "Dorada";
        }
    }

    public string Oznaka => Faza.Length > 0 ? $"{Broj} {Faza}" : Broj;
}

/// <summary>
/// What the "Šta je novo" window shows, newest first. The number is 0.Y.Z: Y counts every new thing since the
/// start and Z every polish or fix, and neither ever goes back to zero. It is worked out here from the lists, so
/// after a change add an entry on top and put the resulting number in Bronxamp.csproj (a test checks they match).
/// Phases: alfa (still taking shape), beta (all functions are in, bugs are being fixed), none from 1.0.0.
/// </summary>
public static class Verzije
{
    public static readonly Verzija[] Sve = Numerisi(
    [
        new("beta", "16. 11. 2026.",
        [
            "Jezik: srpski ili engleski, bira se u Podešavanjima i menja odmah. Prva faza prevoda: glavni prozor (meni, biblioteka, red, prevoz, status) i Podešavanja.",
        ],
        [
        ]),
        new("beta", "14. 11. 2026.",
        [
            "U etru radi kao klasičan plejer: Ctrl+A bira sve, Delete briše izabrano, desni klik nudi „Obriši izabrano“ i „Obriši sve“, a gore je i dugme „Obriši sve“ (dok dan traje, pesma koja svira i odsvirano ostaju).",
        ],
        [
        ]),
        new("beta", "14. 11. 2026.",
        [
            "Dugme „Isprazni dan“: briše emisije, plejliste i hit iz izabranog dana i ostavlja prazan spisak da ga sastaviš iznova (muzika na disku ostaje).",
        ],
        [
        ]),
        new("beta", "14. 11. 2026.",
        [
        ],
        [
            "Red i Ostatak dana su sada samo tih prelaz iz crne u sivu, odozgo nadole — bez linija i sužavanja (korisnik: „skroz bez smisla“).",
        ]),
        new("beta", "14. 11. 2026.",
        [
            "Ostatak dana (desni panel dok je dan isplaniran) je isti 3D usek: pod se sužava u dubinu, vide se zidovi i uglovi, a stavke blede ka dnu — dubina se vidi i tamo gde pesme stvarno idu.",
        ],
        [
            "Ostatak dana se skroluje kad stavki ima više nego što stane, umesto da izlazi iz panela.",
        ]),
        new("beta", "14. 11. 2026.",
        [
            "Red je sada kutija u perspektivi: pod se sužava u dubinu, vide se gornja ivica i oba boka, a pesme blede i tonu u mrak.",
        ],
        [
            "Linije mesta se ne crtaju dok je red prazan, da ne seku tekst obaveštenja.",
        ]),
        new("beta", "14. 11. 2026.",
        [
            "Gornja traka je jedna kartica: sat, šta je u etru i šta sledi, sa tankim pregradama i bojom omota preko cele trake.",
            "Red u koji idu pesme je dublje udubljen, a tanke linije pokazuju mesta (slotove) gde pesme upadaju — i kad je red prazan.",
            "Ekran je mirniji: aktivna stavka menija i tabovi više nisu narandžasti, poruka je mala traka sa trouglom, a pločice albuma su zaobljenije.",
        ],
        [
            "Oznake frekvencija u traci „U etru“ vide se samo dok muzika svira, da prazna traka ne liči na pokvaren lenjir.",
        ]),
        new("beta", "14. 11. 2026.",
        [
            "Red u koji idu pesme je udubljen kao kutija, sa senkom na vrhu i svetlom donjom ivicom, pa se vidi dubina.",
        ],
        [
            "Dok prevlačiš muziku u prozor, ivica reda se oboji narandžasto, da se vidi gde pesme upadaju.",
        ]),
        new("beta", "14. 11. 2026.",
        [
            "Pored svakog foldera bez vremena je polje za vreme: upišeš na primer 15:00 i folder postaje blok koji počinje tada.",
            "Fajlovi u folderu šeme pored hita (emisija u jednom fajlu, na primer Metronom) stoje ispod, svaki sa poljem za vreme početka.",
            "Kod svakog foldera, fajla i bloka piše koliko traje.",
        ],
        [
            "Emisija u jednom fajlu koja je jednom dobila vreme sledeći put se sama vrati u dan sa tim vremenom.",
            "Raspored je poređan po vremenu, a polja za vreme se bolje vide.",
        ]),
        new("beta", "14. 11. 2026.",
        [
            "Sastavi dan je jednostavniji: semafor na vrhu (zeleno kad je sve spremno, žuto sa onim što treba srediti), pa tri koraka - hit dana, šta ide danas i Pokreni dan.",
            "Dan se slaže sam čim nešto promeniš; dugme Složi dan više ne postoji.",
            "Vreme u imenu foldera ili fajla („20.00 Koncertacija“, „9:05 Alternative“, „23h Laku noć“) program upiše sam.",
            "Dan po satima: svaki blok, hit i emisija je jedan red, a klik otvara šta je unutra i koje pesme ne staju.",
            "Folder ili fajl se u dan dodaje i prevlačenjem iz Explorer-a.",
        ],
        [
            "Višak pesama u bloku više nije upozorenje: semafor ostaje zelen, a red bloka kaže koliko ne staje.",
            "Špica, jinglovi za hit, odstupanje i folderi su sklopljeni u delu Napredno; otvori se sam kad nešto od toga fali.",
        ]),
        new("beta", "8. 11. 2026.",
        [
            "U ponoć program sam prekine emitovanje: poslednja pesma dana sme da se završi, a onda na serveru nastavlja njegov Auto DJ — kako je i predviđeno za etar.",
        ],
        [
        ]),
        new("beta", "8. 11. 2026.",
        [
        ],
        [
            "Pusti odmah po pokretanju, dok je red prazan, pušta ono što je na ekranu: spisak od izabrane pesme (ili od prve), otvoren album ili emisiju, a na zidu prvi album. Ranije se moralo prvo kliknuti na pesmu.",
        ]),
        new("beta", "8. 11. 2026.",
        [
            "Spektar u traci U etru radi po hercima kao na ekvilajzeru: 31 stubić od 20 Hz do 20 kHz (po trećina oktave), sa oznakama 50, 100... 1k... 20k ispod.",
        ],
        [
            "Traka U etru je viša i više se ništa ne odseca: naslov i izvođač su tačno na sredini, a proteklo i preostalo vreme su veći brojevi levo i desno, na tamnoj podlozi.",
        ]),
        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Ako server ne prihvati naslov preko BASSenc-a (Zeno ga, izgleda, ne prima), program ga pošalje sam na /admin/metadata i u dnevnik upiše tačan odgovor servera (HTTP kod), pa se vidi gde zapinje.",
        ]),
        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Posle ponovnog povezivanja na server, naziv pesme i izvođač se ponovo pošalju — ranije su se slali samo kad se pesma promeni, pa posle rekontekcije server nije imao šta da prikaže.",
            "U traci „U etru“ ime pesme i izvođač su sada u dva reda (izvođač ispod naslova), pa se izvođač vidi i kad je naslov dug.",
            "Brojevi vremena u traci su krupniji (14) i u tamnim kutijicama u uglovima spektra.",
        ]),
        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Naslov u traci „U etru“ je sada po sredini visine, a spektar je kao ekvilajzer: 32 šira stubića sa vrhovima koji slobodno skaču, umesto 96 gusto zbijenih.",
            "Proteklo i preostalo vreme su u uglovima spektra, u tamnim kutijicama, pa ih naslov i stubići više ne prekrivaju.",
        ]),
        new("beta", "7. 11. 2026.",
        [
            "Traka „U etru“ prerađena po uzoru na RadioBOSS: spektar preko cele širine trake, naslov na sredini, proteklo vreme levo i preostalo vreme desno (LCD brojevi), sitne tačke koje lebde u pozadini — sve staje kad je pauza.",
        ],
        [
            "Spektar ima svoje boje (zeleno dole, žuto pri vrhu visokih stubića), pa se ne meša sa bojom teksta i brojeva (beli naslov, narandžasto proteklo, toplo žuto preostalo).",
        ]),
        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Strim više nije usporen: enkoder dobija frekvenciju miksera (zvučne kartice), a ne fiksno 44.100 Hz — uz 48 kHz karticu pesma je u strimu išla oko 9 % sporije i dublje.",
            "Pri povezivanju se u dnevnik upiše frekvencija miksera i bitrate, pa se vidi šta je poslato lame-u.",
        ]),
        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Kad muzika stane (kraj reda ili pauza), emitovanje više ne piše „veza pukla“ crvenim: program zatvori vezu i čeka muziku („Čeka muziku“), pa se vrati sam kad muzika krene. Dosad je izgledalo kao da server odbija program.",
            "Naziv pesme se šalje serveru samo kad se pesma promeni (dosad se isti naziv slao više puta u sekundi).",
            "U dnevnik grešaka se upisuje i tačan razlog prekida (koje je obaveštenje stiglo i da li je enkoder bio aktivan).",
        ]),
        new("beta", "7. 11. 2026.",
        [
            "Spisak pesama ima kolonu „#“ sa rednim brojem svakog reda (i u biblioteci, i u emisijama, i u omiljenom).",
        ],
        [
            "Uvezene pesme se više ne ređaju po abecedi: red je po folderu, pa po broju iz imena fajla (01, 02, 03…), pa po imenu — numerisan folder zadrži svoj red.",
            "Umesto „Na etru“ svuda piše „U etru“ (meni, traka i pomoć).",
        ]),
        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Emitovanje više ne otvara vezu dok ništa ne svira: traka piše „Čeka muziku“, a veza se otvori čim muzika krene. Do sada je u tom slučaju server posle 15 sekundi izbacivao izvor (nema zvuka), pa je izgledalo kao da server odbija program.",
            "Kad emitovanje ne uspe ili veza pukne, razlog se upisuje u dnevnik grešaka (uz adresu servera).",
        ]),
        new("beta", "7. 11. 2026.",
        [
            "Horizontalni merač nivoa u donjoj traci (levo i desno): zelena traka do −3 dB, a od −3 do 0 dB blago prelazi u crveno; vrh nivoa se kratko zadrži pa padne.",
        ],
        [
        ]),
        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Traka za vezu sa serverom više nije rastegnuta preko celog prozora, a tekstovi su kraći: „Nije povezan“, „Povezan na server“, „Veza pukla — vraćam se…“. Da li je „probno“, piše u opisu kad predješ mišem preko trake.",
        ]),

        new("beta", "7. 11. 2026.",
        [
        ],
        [
            "Donji deo: stanje veze sa serverom je sada jedna traka — tačka, status i dugme „Uključi emitovanje“ / „Prekini emitovanje“ zajedno. Boja trake prati stanje: zelena na etru, žuta dok se povezuje, crvena kad veza pukne.",
        ]),
        new("beta", "7. 11. 2026.",
        [
            "Emitovanje se samo vrati na server: ako veza pukne, program to javi i sam se ponovo poveže, bez tvog klika.",
        ],
        [
            "Stanje je jasnije: „Povezivanje na server…“, „Povezan na server“ (ili „Povezan na server (probno)“) i „Veza pukla — vraćam se na server…“. Detalji se vide kad predješ mišem preko teksta.",
        ]),
        new("beta", "7. 11. 2026.",
        [
            "Dugme „Uključi emitovanje“ u traci: program šalje baš ono što svira na Zeno.fm, a tačka i status (Na etru, Povezivanje, Greška) se stalno vide.",
            "U Podešavanjima, uz Emitovanje na Zeno.fm: kvačica „Probno“ (lokalni server, pravi Zeno se ne dira) i dugme „Test veze“.",
        ],
        [
            "Na etar ide i naziv pesme: pri svakoj promeni pošalje se „Izvođač - Naslov“, pa slušaoci vide šta svira.",
        ]),
        new("beta", "6. 11. 2026.",
        [
            "Emisije imaju svoju stranu, sa spiskom i dugmetom Nova emisija: Biblioteka, pa Emisije.",
        ],
        [
            "Levi meni je kraći: Program, Muzika i Izveštaj. Albumi, Izvođači, Nedavno i Omiljeno su pilule iznad Biblioteke, a Najslušanije i Nije puštano iznad Statistike. Podešavanja, Pomoć i O programu su prikovani za dno.",
        ]),
        new("beta", "6. 11. 2026.",
        [
            "U Podešavanjima je deo Emitovanje na Zeno.fm: adresa, port, mount, korisnik, lozinka i kvalitet. Lozinka je šifrovana, a dugme Prikaži je otkriva.",
        ],
        [
            "Desno stoji Ostatak dana i kad je dan složen a ništa drugo nije u redu, a ne samo dok traje.",
            "Kad se pušta muzika van dana piše Kraj plejliste, a za dan Kraj programa.",
            "Veći razmak u spisku jinglova.",
        ]),
        new("beta", "5. 11. 2026.",
        [
            "Panel ispod liste pokazuje izabranu pesmu: omot, putanju, format, bitrate i frekvenciju.",
            "Dole su dugmad Pusti, Sledeća, Pauza, Stop, ponavljanje i nasumično, broj stavki, kraj i jačina.",
            "Statusna traka na dnu, sa stanjem veze i verzijom.",
        ],
        [
            "Gore su sada sat, polje Na etru sa trakom napretka i polje Sledi, kao u RadioBOSS-u.",
            "Mini traka je izbačena: umanjen program je samo ikonica u taskbaru.",
        ]),
        new("beta", "5. 11. 2026.",
        [
        ],
        [
            "Dan ide bez prelaza između pesama: pesma se završi i kreće sledeća, a vremena se prosto sabiraju.",
        ]),
        new("beta", "5. 11. 2026.",
        [
        ],
        [
            "Program više ne dodaje jinglove da popuni vreme. Jingl ide samo po ritmu, na tri pesme, a blok posle hita kreće odmah.",
        ]),
        new("beta", "5. 11. 2026.",
        [
        ],
        [
            "Program pred kraj bloka sme da pomeri pesme unutar tog bloka, da ne štrče. Iz drugog bloka i dalje ne uzima.",
            "Blok i emisija kreću do 30 sekundi od svog vremena. Veće odstupanje je označeno.",
        ]),
        new("beta", "5. 11. 2026.",
        [
            "Na etru: pesme i jinglovi se pomeraju levim dugmetom miša, prevlačenjem.",
            "Desni klik na stavku: preslušaj na slušalicama, obriši iz plejliste, dodaj jingl ili dodaj pesmu.",
            "Izbor jingla sa pretragom, sa svim jinglovima, i onima koji se ubacuju ručno.",
            "Lista dana može da se menja i dok dan traje, za sve što još nije puštano.",
        ],
        [
            "Program više ne uzima pesme iz sledećeg bloka kad zafali. Kaže koliko minuta fali, a ti dodaš šta hoćeš.",
            "Pesme ostaju redom kojim su u folderu. Program samo odlučuje koliko ih staje pre hita, a ostatak uklapa jinglovima.",
            "Upozorenje da fali pesama nestane čim dodaš dovoljno.",
        ]),
        new("beta", "5. 11. 2026.",
        [
        ],
        [
            "Vreme bloka i emisije je okvirno: kreću odmah posle hita ili pesme, bez dopunskih jinglova. Samo hit dana se drži sata.",
            "Poslednje pesme dana smeju da odu do 15 minuta posle ponoći, umesto da se izostave.",
            "Jinglovi su na listi dana druge boje od pesama.",
        ]),
        new("beta", "5. 11. 2026.",
        [
            "Dugme „Počni iznova“ briše raspored i složen dan za izabrani dan.",
        ],
        [
            "Ispravka: izbor špice i početnih jinglova se gubio kad se ponovo otvori ekran, pa je dan bio složen bez njih.",
            "Ispravka: izabran jingl emisije se brisao na isti način.",
            "Kvačica „Bez prekida“ je uklonjena. Svaki folder u rasporedu dobija jinglove i hit, a gotov fajl se ne dira.",
        ]),
        new("beta", "5. 11. 2026.",
        [
            "Nov gornji deo: traka koja stalno pokazuje stanje dana, sat i šta svira.",
            "Kartice Sledi i Zadato: šta ide sledeće i koliko ima do hita ili emisije.",
            "Ekran Na etru: dan uživo koji prati pesmu, sa prikazima Ceo dan, Samo pesme i Zadata vremena.",
            "Dok dan traje, desno stoji Ostatak dana sa zadatim vremenima i krajem programa.",
            "Folderi: muzika onako kako stoji na disku, sa stablom foldera.",
            "Emitovano: dnevnik onoga što je stvarno odsvirano, po danima.",
            "Desni klik na traku napretka skače na to mesto u pesmi. Dok dan traje, levi klik ne pomera pesmu.",
        ],
        [
            "Levi meni je podeljen na Program, Muzika i Izveštaj.",
            "Logo više nije u gornjem delu, a veliko vreme pokazuje koliko je još ostalo.",
            "Gornji deo je niži, pa spisak ima više mesta.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Plejlista sa vremenom početka je sada blok: unutra idu jinglovi, a hit dana je prekine u svom terminu i ona nastavi.",
            "Kvačica „Bez prekida“ čuva folder u komadu. Gotov fajl se ne dira.",
            "Ako blok ne stane do sledećeg, program kaže koje pesme je izostavio. Ako pesama zafali, sledeći blok kreće ranije.",
        ],
        [
            "Jingl pred hit ili emisiju sme da dođe i posle dve pesme, kad drugačije ne može da se uklopi.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Početak dana biraš ti: špicu, jingl za hit dana i jingl posle hita. Program ih pamti i sam ništa ne bira.",
        ],
        [
            "Jingl ide tačno na svake tri pesme, a nijedan se u toku dana ne ponavlja.",
            "Fajl iz korena foldera sa šemom može da bude i emisija, a ne samo hit dana.",
        ]),
        new("beta", "4. 11. 2026.",
        [
        ],
        [
            "Blok hita dana kreće 15 sekundi pre termina, pa jingl ide u 10:59:45 za hit u 11h.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Novo: „Sastavi dan“. Ti daš pesme iz foldera sa šemom, hit dana i emisije sa vremenom, a program ubaci jinglove i hit dana i složi ceo dan sa tačnim vremenima.",
            "Pokrenut dan kreće sam u 08:59:45, svira po satu, preskače pesme ako kasni i sam nastavlja posle pada programa.",
            "Jinglovi se čitaju iz foldera i sami dele po vrstama, a oznake se mogu ispraviti.",
            "Dnevnik: šta je stvarno odsvirano upisuje se za svaki dan.",
            "Dok dan traje računar ne ide u spavanje, a nasumično i ponavljanje su zaključani.",
        ],
        [
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Nova kolona se pojavi posle trajanja, a prevlačenjem naziva u zaglavlju svaka kolona može da se pomeri gde hoćeš. Raspored se pamti.",
        ],
        [
            "Izvođač i naslov su u spisku pesama dve odvojene kolone.",
            "I izvođač, naslov i trajanje mogu da se isključe.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Desni klik na zaglavlje spiska (ili dugme sa zupčanikom) bira šta se još vidi: album, godina, žanr, broj pesme, puštano, poslednji put, dodato, omiljeno, format, bitrate, frekvencija, veličina, naziv fajla i folder. Širina kolona se menja prevlačenjem.",
            "Izabrane pesme mogu da se uklone iz spiska (desni klik, dugme na redu ili Delete). Fajlovi ostaju na disku, a sve se vraća dugmetom „Vrati uklonjeno“ u Podešavanjima.",
        ],
        [
            "Spisak pesama je zbijen i pokazuje samo „Izvođač - Naslov“ i trajanje.",
            "Redovi u emisijama su takođe gušći.",
            "Pri prvom pokretanju plejer još jednom pročita sve fajlove da bi saznao bitrate i frekvenciju, pa skeniranje potraje malo duže.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Novo dugme „Pomoć“ u levom meniju (i taster F1): kratko uputstvo za sve delove programa.",
            "Novo dugme „O programu“: verzija, korišćene tehnologije i njihove licence.",
        ],
        [
        ]),
        new("beta", "4. 11. 2026.",
        [
        ],
        [
            "Logoi su bez crne pozadine.",
            "Ispod dugmadi za nasumično puštanje i ponavljanje stoji natpis, a uključeno dugme je narandžasto.",
            "Ispravka: kod nasumičnog puštanja album je uvek kretao od prve pesme, sada kreće od bilo koje.",
        ]),
        new("beta", "4. 11. 2026.",
        [
        ],
        [
            "Kad ništa ne svira, u gornjoj traci stoji logo Bronxamp umesto praznog mesta.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Logo Bronxamp: uvodni ekran pri pokretanju i vrh prozora „Šta je novo“.",
            "Logo BronxSoft u Podešavanjima.",
        ],
        [
            "Nova ikonica programa, sa crvenim X iz logoa.",
        ]),
        new("beta", "4. 11. 2026.",
        [
        ],
        [
            "Naziv se piše BronxSoft, sa velikim S: naslov prozora, prečica i svi tekstovi u programu.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Dugmad „Dodaj album“ i „Dodaj pesme“ na glavnom ekranu: muzika se bira sa diska, bez odlaska u Podešavanja.",
            "Muzika se može prevući u prozor iz Explorer-a, i fajlovi i folderi.",
            "Dodata muzika odmah ide u red, a počinje da svira ako ništa ne svira.",
            "Pojedinačne pesme mogu biti bilo gde na disku, ne moraju da budu u folderu biblioteke.",
        ],
        [
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Vidljiva dugmad: kad pređeš mišem preko albuma pojave se Pusti, Pusti sledeće, Dodaj u red, Prikaži pesme i Ukloni.",
            "Isto na pesmama (Pusti sledeće, Dodaj u red), a u redu i u emisiji X briše pesmu.",
            "Album se može ukloniti iz biblioteke. Fajlovi na disku ostaju, a vraća se u Podešavanjima.",
            "Plejer javlja šta je dodato u red i šta sledi.",
        ],
        [
            "Ispravka: omot je znao da nestane sa pločice posle promene veličine prozora.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Broj verzije prati funkcije, program je u beta fazi. Klik na verziju levo dole otvara ovaj prozor.",
            "Red pesama: desni panel prikazuje ceo red. Pesme se brišu tasterom Delete ili desnim klikom, može i ceo album. Dupli klik pušta pesmu.",
            "Dugme „Isprazni“ briše red, a „Sačuvaj kao emisiju“ pravi novu emisiju od njega.",
            "U emisiji su dodati „Ukloni izabrano“, „Isprazni“ i „Ukloni ceo album iz emisije“.",
        ],
        [
            "Program se sada zove BronxSoft Bronxamp: naslov prozora, prečica na desktopu i folder sa podacima. Biblioteka je preneta sama.",
            "Emisije više nisu unapred upisane: praviš samo one koje želiš, dugmetom „Nova emisija“. Prazne početne su uklonjene.",
            "Ispravka: dugme „Sačuvaj“ u Podešavanjima nije radilo, pa se izlazilo samo preko „Otkaži“. Dodat je i X u uglu.",
            "Ispravka: pauza pritisnuta dok se pesma učitava poništavala se, pa je pesma kretala iz početka.",
        ]),
        new("beta", "4. 11. 2026.",
        [
            "Windows integracija: medijski tasteri, kontrole na zaključanom ekranu i u prozoru za jačinu.",
            "Dugmići na sličici na taskbaru i nedavni albumi u listi na desni klik.",
            "Program se pokreće samo jednom: drugo pokretanje samo vraća prozor.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Mini traka iznad taskbara kad se plejer smanji, sa vremenom, spektrom, napretkom i dugmićima.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Statistika: najslušanije ove nedelje i meseca, albumi koji nisu puštani godinu dana.",
            "Preslušavanje na slušalicama, na drugom izlaznom uređaju, dok glavni svira.",
            "Podešavanja: folderi, prelaz, upozorenje, izlazi.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Emisije sa imenom i bojom, spisak pesama i istorija emitovanja.",
            "Upozorenje ako je pesma puštana u istoj emisiji u poslednjih 8 nedelja.",
            "Desni panel „Sledi“ i „KRAJ EMISIJE“: sat kada se emisija završava i preostalo vreme, sa prelazima.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Gornja traka „sada svira“: omot, naslov, LCD vreme, spektar, dugmići i traka napretka.",
            "Boja trake se uzima sa omota.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Zid omota, plus prikazi Lista, A-Š i Godina. Klik na album pušta ga od prve pesme.",
            "Pretraga jednim poljem: „ekv 1983“, „metal 90-e“, bez obzira na kvačice i pismo.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Biblioteka: skeniranje foldera u pozadini i praćenje izmena.",
            "Stari tagovi se čitaju ispravno, a bez tagova se imena uzimaju iz foldera i fajla.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Reprodukcija preko BASS-a: bez prekida između pesama i prelaz do 5 sekundi.",
        ],
        [
        ]),
        new("alfa", "4. 11. 2026.",
        [
            "Specifikacija i dizajn, bez koda.",
        ],
        [
        ]),
    ]);

    private static Verzija[] Numerisi(Verzija[] sve)
    {
        int novih = 0, dorada = 0;
        foreach (var v in sve.Reverse())
        {
            novih += v.Nove.Length;
            dorada += v.Dorade.Length;
            v.Broj = $"0.{novih}.{dorada}";
        }
        return sve;
    }

    public static string Trenutna => Sve[0].Oznaka;

    public static string BrojPrograma => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
}
