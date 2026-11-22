# Týždenná správa

**Obdobie:** 16. – 22. novembra 2026  
**Projekt:** BangAIAgentSimulator-UNI

Počas týždňa som sa venoval dotrénovaniu existujúcich agentov pre hru BANG!, porovnaniu pôvodných a nových modelov a rozšíreniu systému hodnotenia.

## 1. Dotrénovanie agentov

Načítal som existujúce modely, pokračoval v ich trénovaní a ukladal ich pod novými názvami. Dotrénované verzie zahŕňali sheriff_03.pth, bandit_05.pth, bandit_01_1.pth, bandit_02_01.pth a deputy_02_1.pth. Cieľom bolo overiť, či ďalšie trénovanie zlepšilo ich správanie a úspešnosť.

Pôvodné aj dotrénované modely som následne zaradil do spoločného hodnotenia. Agenti pracovali v **evaluation mode**, bez aktualizácie váh. Každá hra mala päť hráčov — Sheriff, Deputy, dvoch Bandits a Renegade. Výsledok sa započítaval všetkým zúčastneným uloženým agentom vrátane spoluhráčov vo víťaznej frakcii.

## 2. Výsledky hodnotenia

### 2.1 Úspešnosť dotrénovaných verzií

Pri hodnotení na **2000 zápasoch** dosiahli dotrénované verzie tieto výsledky:

| Model | Výhry | Odohrané hry | Win rate | 95 % interval spoľahlivosti |
|---|---:|---:|---:|---:|
| sheriff_03.pth | 295 | 673 | **43,8 %** | 35,4 – 52,5 % |
| bandit_05.pth | 325 | 526 | **61,8 %** | 52,1 – 70,9 % |
| bandit_01_1.pth | 323 | 564 | 57,3 % | 47,8 – 66,4 % |
| bandit_02_01.pth | 326 | 551 | 59,2 % | 49,6 – 68,3 % |
| deputy_02_1.pth | 218 | 654 | 33,3 % | 25,5 – 41,9 % |

Počet hier jednotlivých modelov sa líšil, pretože sa náhodne vyberali do zostáv. Jeden zápas poskytoval výsledky viacerým agentom.

- **Sheriff:** Najvyššiu úspešnosť dosiahol sheriff_03.pth. Staršie verzie sheriff_02.pth a sheriff_01.pth dosiahli 34,8 % a 22,9 %.
- **Bandit:** Na prvom mieste zostal bandit_02.pth s 62,1 %, tesne pred dotrénovaným bandit_05.pth.
- **Deputy:** Výsledky jednotlivých verzií boli podobné, približne 33 až 34 %.

Pri viacerých dvojiciach sa intervaly spoľahlivosti prekrývali, preto samotné poradie nepostačovalo na potvrdenie zlepšenia. Rozdiely medzi modelmi som posudzoval v rámci rovnakej roly.

### 2.2 Overenie uložených modelov

Pri interpretácii výsledkov som preveril aj uložené súbory. **Identické tensor data** obsahovali tieto dvojice:

| Pôvodný model | Súbor po dotrénovaní |
|---|---|
| bandit_01.pth | bandit_01_1.pth |
| bandit_02.pth | bandit_02_01.pth |
| deputy_02.pth | deputy_02_1.pth |

Rozdielne výsledky týchto dvojíc vznikli v odlišných zápasoch, hoci ich uložené váhy boli identické.

S tým súvisí zistené správanie **autosave**. Model sa automaticky ukladá pri prekonaní najlepšieho priemerného reward. Ak je prvá hra víťazná, priemer môže dosiahnuť maximum 1,0. Ďalšie trénovanie ho už nemôže prekonať a uložený súbor môže zostať skorou kópiou načítaného modelu. Výsledky týchto súborov preto nepotvrdzujú, že sa do nich uložili váhy po celom dotrénovaní.

## 3. Metódy hodnotenia

Systém som rozšíril o päť metód. Všetky využívajú výsledky zúčastnených uložených agentov po každom zápase. Počet hier a úspešnosť sa evidujú pre každý model samostatne.

### Fixed games

Odohrá presne zadaný počet zápasov a zoradí agentov podľa win rate v rámci ich roly. Poskytuje porovnanie pri pevnom rozpočte hier, bez predčasného ukončenia podľa štatistickej presnosti.

### Uncertainty

K win rate každého agenta počíta 95 % interval spoľahlivosti. Výpočet zohľadňuje hodnotenie viacerých agentov aj opakované priebežné kontroly. Hodnotenie sa skončí pri dosiahnutí požadovanej presnosti pre sledované roly alebo po vyčerpaní limitu zápasov.

### Comparison

Porovnáva rozdiely win rate medzi modelmi rovnakej roly pomocou intervalov spoľahlivosti. Umožňuje vyhodnotiť, či je jeden model lepší, alebo či sú modely v rámci zvolenej tolerancie, napríklad piatich percentuálnych bodov. Ak na takýto záver do dosiahnutia limitu nestačia výsledky, porovnanie zostane označené ako neisté.

### Elo

Používa adaptovanú tímovú verziu Elo pre tri frakcie — Sheriff s Deputy, Bandits a Renegade. Agenti začínajú na 1500 bodoch, používa sa faktor K = 32 a rating sa mení po každom ukončenom zápase podľa skutočného výsledku a očakávanej sily frakcií. Sila frakcie vychádza z priemerného ratingu jej členov, zmena sa rozdeľuje medzi spoluhráčov a poradie sa zobrazuje samostatne pre jednotlivé roly.

### Overall Elo

Používa rovnaký výpočet ako Elo, ale vytvára **jednu spoločnú tabuľku všetkých agentov**. Pri každom modeli zostáva zobrazená jeho rola a win rate. Na porovnanie medzi rôznymi rolami vplývajú aj ich odlišné podmienky a náročnosť víťazstva.

## 4. Zostavy a priebeh zápasov

- **Self-play:** Všetkých päť hráčov tvoria uložené modely zo zvoleného priečinka.
- **Mixed:** Približne 90 % zápasov tvoria iba uložené modely. Približne 5 % obsahuje jedného Random agenta a 5 % jedného Scripted agenta; zvyšné miesta obsadia uložené modely.

Modely sa načítajú pri prvom použití a medzi zápasmi zostávajú v pamäti. Každý uložený agent dostane za zápas jeden výsledok. Ak rovnaký model obsadí obe miesta Bandit, započíta sa raz, aby sa jeden spoločný výsledok nepovažoval za dve nezávislé pozorovania.

## 5. Integrácia do GUI

Pridal som samostatné okno **Agent Ranking** s výberom priečinka modelov, metódy, skupiny súperov a celkového limitu zápasov. Nastavenie **Focus role** určuje sledované roly pre metódy Uncertainty a Comparison.

Tabuľka priebežne zobrazuje výhry, počet hier, win rate, intervaly spoľahlivosti alebo Elo. Doplnil som informácie o načítavaní modelov, aktuálnom priebehu a možnosť **Cancel**. Nastavenia som usporiadal do prehľadných riadkov.

## 6. Výkon a integrácia zmien

Porovnal som CPU a CUDA pri postupnom vyhodnocovaní malých vstupov. V tomto spôsobe vykonávania bola CUDA pomalšia, preto som obnovil CPU. Odstránil som parallel workers po zistení, že pri krátkom meranom behu nepriniesli zrýchlenie. Pri evaluation som obmedzil nepotrebné výpočty a upravil komunikáciu medzi C# a Python.

Zmeny som uložil do commitu a vykonal **Merge** lokálnej main do vetvy Vladyslav-Chechel. Kontrola rozdielov potvrdila zachovanie úprav systému ranking.

