# Týždenná správa

**Obdobie:** 23. – 29. novembra 2026  
**Stav správy:** 25. novembra 2026  
**Projekt:** BangAIAgentSimulator-UNI

Počas týždňa som rozšíril využitie Elo pri tréningových sessions a následnom hodnotení agentov. Dotrénoval som modely proti Scripted súperom aj proti už natrénovaným agentom. Výsledky som porovnal pomocou rebríčka podľa rolí a spoločného Overall Elo.

## 1. Elo počas tréningu

Do hlavného okna simulátora som pridal tabuľku **Session Elo**, ktorá zaznamenáva rating, počet odohraných hier, režim a win rate každého účastníka. Rating sa aktualizuje po každom zápase a tabuľka sa obnovuje každých 100 hier a po skončení session.

Každá nová session začína na **1500 Elo**. Výpočet používa faktor **K = 32** a zohľadňuje tri frakcie — Sheriff s Deputy, dvoch Bandits a samostatného Renegade. Sila frakcie sa počíta z priemerného ratingu jej členov. Random a Scripted súperi majú pevný rating 1500.

Training a následný ranking používajú spoločnú implementáciu výpočtu Elo. V okne Ranking som doplnil **Session Elo** a **Elo Δ**, ktoré umožňujú pri zhodnom súbore modelu porovnať referenciu zo session s evaluation ratingom. Elo Δ predstavuje evaluation Elo mínus Session Elo.

## 2. Tréning nových verzií agentov

### 2.1 Tréning proti natrénovaným agentom

V jednotlivých sessions sa trénoval jeden agent a ostatní účastníci používali uložené modely v evaluation mode. Každý zdokumentovaný beh mal **3000 hier** a päť hráčov.

| Trénovaný model | Rola | Počet hier | Win rate počas session | Session Elo |
|---|---|---:|---:|---:|
| sheriff_01_01.pth | Sheriff | 3000 | 35,2 % | 1664 |
| bandit_06_01.pth | Bandit | 3000 | 55,7 % | 1685 |
| deputy_01_01.pth | Deputy | 3000 | 73,8 % | 1825 |
| renegade_01_01.pth | Renegade | 3000 | 1,8 % | 1132 |

Najvyššiu úspešnosť z týchto behov dosiahol trénovaný Deputy. Renegade mal výrazne nižší podiel výhier. Keďže každá rola má iné podmienky víťazstva a sessions mali odlišné zostavy, tieto percentá vyjadrujú výsledky konkrétnych behov.

Podklady k jednotlivým sessions sú v prílohách [Sheriff](New%20folder/Screenshot%202026-11-25%20224016.png), [Bandit](New%20folder/Screenshot%202026-11-25%20225048.png), [Deputy](New%20folder/Screenshot%202026-11-25%20230107.png) a [Renegade](New%20folder/Screenshot%202026-11-25%20231259.png).

### 2.2 Tréning proti Scripted agentom

V ďalšej session som trénoval **bandit_01.pth** proti štyrom Scripted agentom. Po 3000 hrách dosiahol **69,3 % výhier a Session Elo 2251**. Scripted účastníci zostali na ratingu 1500.

Tento beh umožnil sledovať správanie modelu proti pevne definovaným stratégiám a porovnať ho s jeho následným výkonom medzi uloženými agentmi.

![Session s trénovaným Bandit agentom a štyrmi Scripted súpermi](New%20folder/Screenshot%202026-11-25%20231756.png)

## 3. Následné hodnotenie podľa rolí

Po tréningu som spustil samostatné evaluation hodnotenie uložených modelov. V rebríčku **Elo** sa agenti zoradili v rámci svojich rolí. Výsledok každého zápasu sa započítal všetkým zúčastneným uloženým agentom.

| Rola | Agent na prvom mieste | Elo | Win rate |
|---|---|---:|---:|
| Sheriff | sheriff_01_01.pth | 1930 | 72,5 % |
| Deputy | deputy_01_01.pth | 1686 | 71,4 % |
| Bandit | bandit_06.pth | 1606 | 28,7 % |
| Renegade | renegade_01.pth | 912 | 0,6 % |

V tomto behu sa dotrénovaný Sheriff umiestnil pred pôvodným sheriff_01.pth, ktorý dosiahol Elo 1831 a win rate 71,4 %. Dotrénovaný Deputy mal vyššie Elo než pôvodný deputy_01.pth, hoci ich win rates boli veľmi podobné, 71,4 % a 71,6 %.

Nový bandit_06_01.pth dosiahol Elo 1593 a win rate 28,6 %, teda podobný výsledok ako pôvodný bandit_06.pth. Nový renegade_01_01.pth skončil s Elo 895 a win rate 0,4 %.

Pri Renegade sa zobrazilo aj priame porovnanie so session, v ktorej sa trénoval. Referenčné Session Elo bolo 1132 a evaluation Elo 895, takže **Elo Δ bolo −237**. Tento rozdiel opisuje výsledok v dvoch odlišných podmienkach hodnotenia.

![Elo rebríček podľa rolí a porovnanie so Session Elo](New%20folder/Screenshot%202026-11-25%20231515.png)

## 4. Spoločný rebríček Overall Elo

Samostatne som spustil **Overall Elo** s limitom **2000 zápasov** a skupinou súperov **Mixed**. Tá používa približne 90 % zápasov iba s uloženými agentmi a približne 10 % zápasov s jedným Random alebo Scripted účastníkom.

Overall Elo používa rovnakú tímovú formulu ako Elo, ale vytvára jedno spoločné poradie všetkých rolí.

| Poradie | Agent | Rola | Elo | Win rate |
|---:|---|---|---:|---:|
| 1 | sheriff_01.pth | Sheriff | 1892 | 72,2 % |
| 2 | sheriff_01_01.pth | Sheriff | 1821 | 71,1 % |
| 3 | deputy_01_01.pth | Deputy | 1725 | 72,6 % |
| 4 | deputy_011.pth | Deputy | 1657 | 69,3 % |
| 5 | bandit_06.pth | Bandit | 1631 | 29,4 % |
| 6 | deputy_01.pth | Deputy | 1627 | 70,4 % |
| 7 | bandit_06_01.pth | Bandit | 1592 | 28,6 % |
| 8 | bandit_01.pth | Bandit | 1480 | 28,3 % |
| 9 | renegade_01.pth | Renegade | 974 | 0,6 % |
| 10 | renegade_01_01.pth | Renegade | 911 | 0,5 % |

V tomto behu viedol pôvodný sheriff_01.pth. Dotrénovaný deputy_01_01.pth sa umiestnil nad ostatnými modelmi rovnakej roly.

Oproti predchádzajúcemu evaluation behu bol do zostavy dostupných modelov pridaný aj bandit_01.pth. Zmena súperov a odlišné výsledky zápasov preto ovplyvnili ratingy aj poradie. Rozdiel medzi tabuľkami nevznikol iba prepnutím spôsobu zobrazenia rebríčka. Na spoločné poradie zároveň vplýva odlišná náročnosť jednotlivých rolí.

![Overall Elo so spoločným poradím všetkých agentov](New%20folder/Screenshot%202026-11-25%20232456.png)

## 5. Vyhodnotenie experimentov

**Výsledok proti Scripted súperom sa nepreniesol priamo do zmiešaného hodnotenia.** Bandit_01 dosiahol pri tréningu proti Scripted agentom 69,3 % výhier a Elo 2251, zatiaľ čo v Overall Elo medzi uloženými modelmi získal 28,3 % výhier a Elo 1480. Ukázal sa tak výrazný rozdiel medzi týmito dvoma skupinami súperov.

Pri dotrénovaných modeloch sa neprejavilo jednotné zlepšenie vo všetkých rolách. Deputy_01_01 sa v oboch evaluation rebríčkoch umiestnil vyššie než pôvodný deputy_01. Poradie oboch Sheriff verzií sa medzi behmi vymenilo, Bandit verzie mali podobné výsledky a dotrénovaný Renegade zostal pod pôvodnou verziou.

Session Elo zachytáva priebeh hrania s meniacou sa politikou počas tréningu. Následný ranking hodnotí uložené modely v evaluation mode a s inou skladbou súperov. Porovnanie preto pomohlo odhaliť rozdiely medzi tréningovým prostredím a samostatným hodnotením; samotná hodnota Elo Δ nepotvrdzuje zlepšenie alebo zhoršenie modelu pri rovnakých podmienkach.
