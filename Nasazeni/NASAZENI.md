# Nasazení SPSSMENYRA – rozpis služeb R.A. / ATK

Jeden program pro obě varianty. Variantu určuje parametr při spuštění:

| Varianta | Parametr | Tabulky | Role pro vstup |
|---|---|---|---|
| R.A. služby | `RA` (výchozí) | dr0, dr1, tr2, tr3, řada DRA | 002, 100 (správce 001 vždy) |
| ATK služby | `ATK` | da0, da1, ta2, ta3, řada DAA | 003, 100 (správce 001 vždy) |

Tabulky jsou v databázi Sportis již převedené z VFP, program žádné nové nezakládá (kromě `jis_log`, pokud chybí).

## 1. Sestavení a kopie programu
1. V `C:\VSPROJECT\SPSSMENYRA` spustit `copy_to_JisApp.bat`.
   Sestaví Release a zkopíruje ho do `C:\JisApp\SPSSMENYRA\SPSSMENYRA_<verze>` (verze z `version.txt`).
2. Obsah této složky nakopírovat na produkční PC do adresáře programu (stejně jako u SpsLite).

## 2. Sestavy (tisky)
Program bere sestavy ze **společné** složky `C:\JisApp\Reports\` (ne ze složky programu).
Zkopírovat do ní:
- `SmenyRozpis.rdlc`
- `SmenyPrehled.rdlc`

(jsou ve složce `Reports` sestaveného programu i v projektu `SPSSMENYRA\Reports`).

## 3. Konfigurace programu
Vytvořit `C:\JisApp\SPSSMENYRA\JisConfig.json` – vzor je `Nasazeni\JisConfig.json`.

- `StartIdConnection` – nastavit na **produkční** připojení k databázi Sportis (stejné jako má SpsLite v `C:\JisApp\SpsLite\JisConfig.json`); ve vzoru je testovací `SportisLocal`.
- `Login` = 1 (přihlášení), `SingleInstance` = 0 (aby šlo spustit R.A. a ATK současně).

**Bez tohoto souboru** program použije obecný `C:\JisApp\JisConfig.json` – připojí se jinam (MS SQL) a skončí chybou SQL.

## 4. Konfigurace gridů v databázi
V produkční databázi Sportis spustit `Nasazeni\smeny_gridy.sql`, např.:

```
mysql -u <uzivatel> -p -D Sportis --default-character-set=utf8mb4 -e "source C:/VSPROJECT/SPSSMENYRA/Nasazeni/smeny_gridy.sql"
```

Zapíše gridy SMENY_ROZPIS, SMENY_PRACOVNICI, SMENY_SUMARIZACE a SMENY_PRAC_SEZNAM (wgrids + wgridit). Skript lze spustit opakovaně – původní záznamy SMENY_* nejdřív smaže, klíče přidělí za nejvyšším existujícím.
Na konci vypíše počty sloupců: 10, 7, 17, 10.

## 5. Zástupci na ploše
- **R.A. služby** – cíl: `"<adresář programu>\SPSSMENYRA.exe" RA`
- **ATK služby** – cíl: `"<adresář programu>\SPSSMENYRA.exe" ATK`

## 6. Kontrola po nasazení
1. Spustit R.A. – titulek okna „Sportis, R.A. Služby“, přihlásit se uživatelem s rolí 002.
2. ROZPIS – zobrazí se rozpis aktuálního měsíce a součty vpravo.
3. TISK – otevře se náhled rozpisu.
4. Totéž pro ATK (role 003).

## Poznámky
- Uživatel bez role varianty program po přihlášení nespustí (hlášení „nemá oprávnění“).
- Program a VFP pracují se stejnými tabulkami. Souběžný provoz je možný, jen by se týž měsíc neměl upravovat zároveň v obou.
- Chyby programu se zapisují do tabulky `jis_log` a do `C:\JisApp\SPSSMENYRA\Err\<datum>.txt`.
