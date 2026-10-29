using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SPSSMENYRA
{
    /// <summary>
    /// pracovnik (tabulka t3 - tr3 / ta3) a jeho planovana dovolena na rok
    /// </summary>
    public class PracovnikModel
    {
        public string Ft301 { get; set; } = "";        // klic
        public string Ft302 { get; set; } = "";        // jmeno (zobrazovane v rozpisu)
        public string Ft303 { get; set; } = "";        // cele jmeno
        public decimal Ft304 { get; set; }             // uvazek
        public DateTime? Ft305 { get; set; }           // datum narozeni
        public string Ft306 { get; set; } = "";        // telefon
        public string Ft307 { get; set; } = "";        // typ (t1 SPORT_OSTYP: B brigadnik, K kmenovy)
        public string F0 { get; set; } = "";           // stav (t1 SPORT_OSSTAV: '' aktivni, 1 vyrazen)
        public string Ft309 { get; set; } = "";        // poznamka
        public decimal Dovp { get; set; }              // planovana dovolena v roce (d1.f102 = '1')
        public decimal Dovv { get; set; }              // vycerpana dovolena v roce (d1.f102 = '2')
    }

    /// <summary>
    /// Pracovnici a dovolena (VFP SPORT1 karta pracovnika, SPORT11 cerpani dovolene) - bez dialogu, zapisy v transakci.
    /// <br>Pri chybe vyhodi JisException: s textem = upozorneni pro uzivatele, bez textu = chyba uz byla ohlasena (databaze).</br>
    /// <br>Dovolena (d1 - dr1 / da1): f102 = '1' plan na rok (f104 = 1.1. roku, f103 = dnu), f102 = '2' cerpani (radek za den, f103 = 1 nebo 0.5).</br>
    /// </summary>
    public static class PracovnikSluzba
    {
        public const string Plan = "1";
        public const string Cerpani = "2";

        /// <summary>
        /// nacte pracovnika s dovolenou na rok (VFP SPORT1_TMP), prazdny klic = novy
        /// </summary>
        public static PracovnikModel Nacti(string ft301, int rok)
        {
            var p = new PracovnikModel();
            if (string.IsNullOrWhiteSpace( ft301 ))
                return p;

            if (!SpravaDat.Select( "_t3karta", $"SELECT t3.* FROM {Smena.T3} t3 WHERE t3.ft301 = @ft301", new { ft301 }, hide: true ))
                throw new JisException();
            DataRow r = Goa.TableEnum( "_t3karta" ).FirstOrDefault();
            if (r == null)
                throw new JisException( "Pracovník nebyl nalezen" );

            p.Ft301 = $"{r[ "ft301" ]}".Trim();
            p.Ft302 = $"{r[ "ft302" ]}".Trim();
            p.Ft303 = $"{r[ "ft303" ]}".Trim();
            p.Ft304 = Convert.ToDecimal( r[ "ft304" ] );
            p.Ft305 = r[ "ft305" ] is DateTime d && d.Year > 1900 ? d : (DateTime?)null;
            p.Ft306 = $"{r[ "ft306" ]}".Trim();
            p.Ft307 = $"{r[ "ft307" ]}".Trim();
            p.F0 = $"{r[ "f0" ]}".Trim();
            p.Ft309 = $"{r[ "ft309" ]}".TrimEnd();
            p.Dovp = Dovolena( p.Ft301, rok, Plan );
            p.Dovv = Dovolena( p.Ft301, rok, Cerpani );
            return p;
        }

        /// <summary>
        /// soucet dovolene pracovnika v roce - plan nebo cerpani
        /// </summary>
        public static decimal Dovolena(string ft301, int rok, string f102)
        {
            object x = SpravaDat.Scalar( $"SELECT IFNULL(SUM(d1.f103), 0) FROM {Smena.D1} d1 WHERE d1.ft301 = @ft301 AND d1.f102 = @f102 AND YEAR(d1.f104) = @rok",
                new { ft301, f102, rok } );
            if (x == null)
                throw new JisException();
            return Convert.ToDecimal( x );
        }

        /// <summary>
        /// vek k dnesku (VFP SPORT1_VEK)
        /// </summary>
        public static int Vek(DateTime? narozeni)
        {
            if (narozeni == null)
                return 0;
            DateTime n = narozeni.Value.Date;
            int vek = DateTime.Today.Year - n.Year;
            if (n > DateTime.Today.AddYears( -vek ))
                vek--;
            return Math.Max( vek, 0 );
        }

        /// <summary>
        /// ulozi pracovnika a planovanou dovolenu na rok (VFP SPORT1_SAVE) - novy dostane klic az pri ulozeni
        /// <br>VFP plan 0 neukladal (zustala puvodni hodnota) - zde se ulozi i 0, novy zaznam planu jen pro nenulovy</br>
        /// </summary>
        public static void Uloz(PracovnikModel p, int rok)
        {
            p.Ft302 = (p.Ft302 ?? "").Trim();
            p.Ft303 = (p.Ft303 ?? "").Trim();
            p.Ft306 = (p.Ft306 ?? "").Trim();
            if (string.IsNullOrEmpty( p.Ft303 ))
                p.Ft303 = p.Ft302;                  // VFP SPORT1_FT302VALID

            if (string.IsNullOrEmpty( p.Ft307 ))
                throw new JisException( "Nelze uložit - není zadán TYP !" );
            if (string.IsNullOrEmpty( p.Ft302 ))
                throw new JisException( "Nelze uložit - není zadáno JMÉNO !" );
            if (p.Ft306 != "" && p.Ft306.Count( char.IsDigit ) < 9)
                throw new JisException( "Nelze uložit - telefonní číslo není úplné (musí mít min. 9 číslic) !" );
            if (p.Ft304 < 0 || p.Dovp < 0)
                throw new JisException( "Nelze uložit - úvazek ani dovolená nesmí být záporné !" );

            using (var tr = SpravaDat.Transakce())
            {
                // stejne jmeno (zobrazovane v rozpisu) - VFP kontroloval jen u noveho
                object pocet = SpravaDat.Scalar( $"SELECT COUNT(*) FROM {Smena.T3} t3 WHERE t3.ft302 = @ft302 AND t3.ft301 <> @ft301",
                    new { p.Ft302, ft301 = p.Ft301 ?? "" } );
                if (pocet == null)
                    throw new JisException();
                if (Convert.ToInt32( pocet ) > 0)
                    throw new JisException( "Osobu se stejným jménem již systém obsahuje" );

                bool novy = string.IsNullOrEmpty( p.Ft301 );
                string ft301 = novy ? NovyKlic( "FT301" ) : p.Ft301;
                var hodnoty = new
                {
                    ft301,
                    f0 = p.F0 ?? "",
                    ft302 = p.Ft302,
                    ft303 = p.Ft303,
                    ft304 = p.Ft304,
                    ft305 = p.Ft305?.Date,
                    ft306 = p.Ft306,
                    ft307 = p.Ft307,
                    ft309 = p.Ft309 ?? ""
                };
                bool ok = novy
                    ? SpravaDat.Exec( $@"
                        INSERT INTO {Smena.T3} (f0, f1, ft301, ft302, ft303, ft304, ft305, ft306, ft307, ft308, ft309)
                        VALUES (@f0, '', @ft301, @ft302, @ft303, @ft304, @ft305, @ft306, @ft307, '', @ft309)", hodnoty )
                    : SpravaDat.Exec( $@"
                        UPDATE {Smena.T3} SET f0 = @f0, ft302 = @ft302, ft303 = @ft303, ft304 = @ft304, ft305 = @ft305,
                            ft306 = @ft306, ft307 = @ft307, ft309 = @ft309
                        WHERE ft301 = @ft301", hodnoty );
                if (!ok)
                    throw new JisException();

                UlozPlan( ft301, rok, p.Dovp );
                tr.Commit();
                p.Ft301 = ft301;
            }
        }

        static void UlozPlan(string ft301, int rok, decimal dnu)
        {
            DateTime f104 = new DateTime( rok, 1, 1 );
            object f101 = SpravaDat.Scalar( $"SELECT d1.f101 FROM {Smena.D1} d1 WHERE d1.ft301 = @ft301 AND d1.f102 = '1' AND d1.f104 = @f104 FOR UPDATE",
                new { ft301, f104 } );
            if (f101 == null && dnu == 0)
                return;

            var hodnoty = new { f101 = f101 == null ? NovyKlic( "F101" ) : $"{f101}".Trim(), ft301, f103 = dnu, f104, ukey = Goa.Ukey ?? "" };
            bool ok = f101 == null
                ? SpravaDat.Exec( $@"
                    INSERT INTO {Smena.D1} (f0, f1, f01, f101, ft301, f102, f103, f104, f02, f03)
                    VALUES ('', '', '', @f101, @ft301, '1', @f103, @f104, @ukey, NOW())", hodnoty )
                : SpravaDat.Exec( $"UPDATE {Smena.D1} SET f103 = @f103, f02 = @ukey, f03 = NOW() WHERE f101 = @f101", hodnoty );
            if (!ok)
                throw new JisException();
        }

        /// <summary>
        /// zapise cerpani dovolene za kazdy den rozsahu (1 den), dny uz zapsane preskoci - vraci pocet novych dnu (VFP SPORT11_SAVE)
        /// <br>soboty a nedele se zapisuji take (ve VFP bylo vynechani zakomentovane)</br>
        /// </summary>
        public static int PridatCerpani(string ft301, DateTime od, DateTime @do)
        {
            if (string.IsNullOrWhiteSpace( ft301 ))
                throw new JisException( "Pracovník není uložen" );
            if (@do.Date < od.Date)
                throw new JisException( "Chybně zadaný rozsah dovolené" );
            if ((@do.Date - od.Date).Days > 60)
                throw new JisException( "Rozsah dovolené je delší než 60 dnů - zkontrolujte zadání" );

            int pocet = 0;
            using (var tr = SpravaDat.Transakce())
            {
                for (DateTime den = od.Date; den <= @do.Date; den = den.AddDays( 1 ))
                {
                    object existuje = SpravaDat.Scalar( $"SELECT COUNT(*) FROM {Smena.D1} d1 WHERE d1.ft301 = @ft301 AND d1.f102 = '2' AND d1.f104 = @den",
                        new { ft301, den } );
                    if (existuje == null)
                        throw new JisException();
                    if (Convert.ToInt32( existuje ) > 0)
                        continue;

                    if (!SpravaDat.Exec( $@"
                        INSERT INTO {Smena.D1} (f0, f1, f01, f101, ft301, f102, f103, f104, f02, f03)
                        VALUES ('', '', '', @f101, @ft301, '2', 1, @den, @ukey, NOW())",
                        new { f101 = NovyKlic( "F101" ), ft301, den, ukey = Goa.Ukey ?? "" } ))
                        throw new JisException();
                    pocet++;
                }
                tr.Commit();
            }
            return pocet;
        }

        /// <summary>
        /// pocet dnu cerpani za jeden den - nejvyse 1 (VFP SPORT11_F103_VALID)
        /// </summary>
        public static void ZmenitCerpani(string f101, decimal dnu)
        {
            if (dnu <= 0 || dnu > 1)
                throw new JisException( "NELZE ULOŽIT\n\nPočet dnů za jeden den může být nejvýše 1" );
            if (!SpravaDat.Exec( $"UPDATE {Smena.D1} SET f103 = @dnu, f02 = @ukey, f03 = NOW() WHERE f101 = @f101 AND f102 = '2'",
                new { dnu, f101, ukey = Goa.Ukey ?? "" } ))
                throw new JisException();
        }

        /// <summary>
        /// smaze dny cerpani dovolene (VFP SPORT11_DEL)
        /// </summary>
        public static void SmazatCerpani(IEnumerable<string> f101)
        {
            var klice = f101.Where( k => !string.IsNullOrWhiteSpace( k ) ).ToList();
            if (klice.Count == 0)
                throw new JisException( "Není vybrán žádný den dovolené" );
            using (var tr = SpravaDat.Transakce())
            {
                foreach (string k in klice)
                    if (!SpravaDat.Exec( $"DELETE FROM {Smena.D1} WHERE f101 = @f101 AND f102 = '2'", new { f101 = k } ))
                        throw new JisException();
                tr.Commit();
            }
        }

        /// <summary>
        /// novy klic z rady DRA / DAA (VFP _XGENKEY 'FT301' / 'F101', 'dra' / 'daa')
        /// </summary>
        static string NovyKlic(string rada)
        {
            string klic = LibSys.GenKey( Smena.DA, rada );
            if (string.IsNullOrWhiteSpace( klic ))
                throw new JisException( $"Chyba při generování klíče {rada} - nelze uložit !" );
            return klic;
        }
    }
}
