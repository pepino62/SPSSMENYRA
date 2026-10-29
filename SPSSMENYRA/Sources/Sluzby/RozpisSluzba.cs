using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SPSSMENYRA
{
    /// <summary>
    /// Rozpis sluzeb (tabulka d0 - dr0 / da0) - bez dialogu, zapisy v transakci.
    /// <br>Pri chybe vyhodi JisException: s textem = upozorneni pro uzivatele, bez textu = chyba uz byla ohlasena (databaze).</br>
    /// <br>Radky d0: f003 = '1' casovy radek (f00n1 = kod t2, f00n2 = hodiny t2), f003 = '2' radek dne (f00n1 = kod t3,
    /// f00n2 = hodiny z casoveho radku, f00n3 = rucne opravene hodiny), f005 = '00' hlavni radek dne, '01'.. viceradek.</br>
    /// <br>Hodiny osob (f00n2) se berou z nejblizsiho predchoziho casoveho radku, ktery ma ve sloupci n vyplneny cas.</br>
    /// </summary>
    public static class RozpisSluzba
    {
        public const string CasovyRadek = "1";
        public const string RadekDne = "2";
        public const string HlavniRadek = "00";
        public const int Sloupcu = 4;

        /// <summary>
        /// existuje pro mesic nejaky zaznam rozpisu ?
        /// <br>VFP SPORT0_XMONTH_VALID testoval jen 1. den mesice</br>
        /// </summary>
        public static bool ExistujeMesic(int rok, int mesic)
        {
            DateTime od = new DateTime( rok, mesic, 1 );
            object pocet = SpravaDat.Scalar( $"SELECT COUNT(*) FROM {Smena.D0} d0 WHERE d0.f004 = 'R' AND d0.f002 BETWEEN @datod AND @datdo",
                new { datod = od, datdo = od.AddMonths( 1 ).AddDays( -1 ) } );
            if (pocet == null)
                throw new JisException();
            return Convert.ToInt32( pocet ) > 0;
        }

        /// <summary>
        /// vygeneruje rozpis mesice - casovy radek na 1. den a radek pro kazdy den (VFP SPORT0_XMONTH_GEN)
        /// </summary>
        public static void GenerovatMesic(int rok, int mesic)
        {
            if (ExistujeMesic( rok, mesic ))
                throw new JisException( $"Rozpis pro měsíc {mesic:00}/{rok} již existuje" );

            DateTime od = new DateTime( rok, mesic, 1 );
            int dnu = DateTime.DaysInMonth( rok, mesic );

            using (var tr = SpravaDat.Transakce())     // cely mesic, nebo nic
            {
                Vloz( od, CasovyRadek, "" );           // VFP SPORT0_XTIME_GEN - f005 prazdne
                for (int i = 0; i < dnu; i++)
                    Vloz( od.AddDays( i ), RadekDne, HlavniRadek );
                tr.Commit();
            }
        }

        /// <summary>
        /// SQL rozpisu za obdobi @datod - @datdo v poradi radku (VFP SPORT0_2TMP_C)
        /// <br>FX1..4 - casovy radek: rozmezi z t2, radek dne: jmeno z t3; FK1..4 - kod (f00n1)</br>
        /// <br>FH1..4 - hodiny: casovy radek f00n2, radek dne f00n3 (rucne), jinak f00n2; FR1..4 = 1 rucne opravene hodiny; FS1..4 = FH (pro soucty i pri filtru)</br>
        /// </summary>
        public static string RozpisSql()
        {
            string sloupce = "";
            string join = "";
            for (int n = 1; n <= Sloupcu; n++)
            {
                sloupce += $@",
                    d0.f00{n}1 AS fk{n},
                    IF(d0.f003 = '1', IFNULL(t2_{n}.ft202, ''), IFNULL(t3_{n}.ft302, '')) AS fx{n},
                    IF(d0.f00{n}1 = '', NULL, IF(d0.f003 = '2' AND d0.f00{n}3 <> 0, d0.f00{n}3, NULLIF(d0.f00{n}2, 0))) AS fh{n},
                    IF(d0.f003 = '2' AND d0.f00{n}1 <> '' AND d0.f00{n}3 <> 0, 1, 0) AS fr{n},
                    IF(d0.f00{n}1 = '', NULL, IF(d0.f003 = '2' AND d0.f00{n}3 <> 0, d0.f00{n}3, NULLIF(d0.f00{n}2, 0))) AS fs{n}";
                join += $@"
                    LEFT JOIN {Smena.T2} t2_{n} ON t2_{n}.ft201 = d0.f00{n}1 AND d0.f003 = '1'
                    LEFT JOIN {Smena.T3} t3_{n} ON t3_{n}.ft301 = d0.f00{n}1 AND d0.f003 = '2'";
            }

            return $@"
                SELECT d0.f001, d0.f002, d0.f003, d0.f005,
                    IF(d0.f003 = '2' AND d0.f005 = '00', DAY(d0.f002), NULL) AS fday,
                    IF(d0.f003 = '2', ELT(DAYOFWEEK(d0.f002), 'Ne', 'Po', 'Út', 'St', 'Čt', 'Pá', 'So'), '') AS fdayc,
                    DAYOFWEEK(d0.f002) AS fdayn{sloupce}
                FROM {Smena.D0} d0{join}
                WHERE d0.f004 = 'R' AND d0.f002 BETWEEN @datod AND @datdo
                ORDER BY d0.f002, d0.f003, d0.f005";
        }

        /// <summary>
        /// SQL hodin a smen pracovniku ze vsech 4 sloupcu radku dne v obdobi (rucne opravene maji prednost)
        /// <br>sloupce ft301, f002, hod, smen (1 za kazdy obsazeny sloupec); od / do = nazvy parametru, napr. "@datod"</br>
        /// </summary>
        public static string HodinySql(string od, string @do)
            => string.Join( " UNION ALL ", Enumerable.Range( 1, Sloupcu ).Select( n => $@"
                SELECT d0.f00{n}1 AS ft301, d0.f002, IF(d0.f00{n}3 <> 0, d0.f00{n}3, d0.f00{n}2) AS hod, 1 AS smen
                FROM {Smena.D0} d0
                WHERE d0.f003 = '2' AND d0.f004 = 'R' AND d0.f00{n}1 <> '' AND d0.f002 BETWEEN {od} AND {@do}" ) );

        #region <<< zmeny v bunce (sloupec n = 1..4) >>>

        /// <summary>
        /// cas ve sloupci casoveho radku - hodiny z t2 a prepocet osob (VFP SPORT0_F00n1_POST)
        /// </summary>
        public static void NastavitCas(string f001, int n, string ft201)
        {
            Zmenit( f001, n, CasovyRadek, r =>
            {
                object hodiny = SpravaDat.Scalar( $"SELECT t2.ft203 FROM {Smena.T2} t2 WHERE t2.ft201 = @ft201", new { ft201 } );
                if (hodiny == null)
                    throw new JisException( "Časové rozmezí nebylo nalezeno" );
                Uloz( f001, $"f00{n}1 = @kod, f00{n}2 = @hodiny", new { kod = ft201, hodiny = Convert.ToDecimal( hodiny ) } );
            } );
        }

        /// <summary>
        /// vymaze cas ve sloupci casoveho radku - osoby prevezmou hodiny z predchoziho casoveho radku (VFP SPORT0_XTI_DELn)
        /// </summary>
        public static void SmazatCas(string f001, int n)
        {
            Zmenit( f001, n, CasovyRadek, r => Uloz( f001, $"f00{n}1 = '', f00{n}2 = 0" ) );
        }

        /// <summary>
        /// pracovnik ve sloupci radku dne - hodiny z casoveho radku, rucne opravene hodiny zustavaji (VFP SPORT0_F00n1_POST)
        /// </summary>
        public static void NastavitOsobu(string f001, int n, string ft301)
        {
            Zmenit( f001, n, RadekDne, r => Uloz( f001, $"f00{n}1 = @kod", new { kod = ft301 } ) );
        }

        /// <summary>
        /// vymaze pracovnika i jeho hodiny (VFP SPORT0_XOS_DELn)
        /// </summary>
        public static void SmazatOsobu(string f001, int n)
        {
            Zmenit( f001, n, RadekDne, r => Uloz( f001, $"f00{n}1 = '', f00{n}2 = 0, f00{n}3 = 0" ) );
        }

        /// <summary>
        /// rucne opravene hodiny pracovnika, 0 = zrusit opravu (VFP SPORT0_F00n3_POST)
        /// </summary>
        public static void NastavitHodiny(string f001, int n, decimal hodiny)
        {
            if (hodiny < 0 || hodiny > 24)
                throw new JisException( "Počet hodin musí být v rozmezí 0 - 24" );
            Zmenit( f001, n, RadekDne, r =>
            {
                if (string.IsNullOrEmpty( r.Kod[ n ] ))
                    throw new JisException( "Ve sloupci není zadán pracovník" );
                Uloz( f001, $"f00{n}3 = @hodiny", new { hodiny } );
            } );
        }

        #endregion

        #region <<< radky >>>

        /// <summary>
        /// vlozi prazdny casovy radek pred den (VFP SPORT0_XTIME_GEN) - jen jeden casovy radek na den
        /// </summary>
        public static void VlozitCasovyRadek(string f001)
        {
            using (var tr = SpravaDat.Transakce())
            {
                Radek r = NactiRadek( f001 );
                if (r.F003 != RadekDne)
                    throw new JisException( "Řádek časového rozvrhu lze vložit jen na řádku dne" );
                if (NactiMesic( r.F002 ).Any( x => x.F003 == CasovyRadek && x.F002 == r.F002 ))
                    throw new JisException( "NELZE VLOŽIT\n\nPro tento den již existuje řádek časového rozvrhu" );
                Vloz( r.F002, CasovyRadek, "" );
                tr.Commit();
            }
        }

        /// <summary>
        /// odstrani casovy radek - osoby prevezmou hodiny z predchoziho casoveho radku (VFP SPORT0_XTIME_DEL)
        /// <br>prvni casovy radek mesice nelze odstranit</br>
        /// </summary>
        public static void OdstranitCasovyRadek(string f001)
        {
            using (var tr = SpravaDat.Transakce())
            {
                Radek r = NactiRadek( f001 );
                if (r.F003 != CasovyRadek)
                    throw new JisException( "Vybraný řádek není řádkem časového rozvrhu" );
                List<Radek> mesic = NactiMesic( r.F002 );
                int i = mesic.FindIndex( x => x.F001 == f001 );
                if (i == 0)
                    throw new JisException( "První časový rozvrh nelze vymazat !" );

                if (!SpravaDat.Exec( $"DELETE FROM {Smena.D0} WHERE f001 = @f001", new { f001 } ))
                    throw new JisException();
                mesic.RemoveAt( i );
                Prepocitat( mesic, i );
                tr.Commit();
            }
        }

        /// <summary>
        /// vlozi dalsi radek dne (viceradek) - f005 = nejvyssi + 1 (VFP SPORT0_XDAY_GEN)
        /// </summary>
        public static void VlozitViceradek(string f001)
        {
            using (var tr = SpravaDat.Transakce())
            {
                Radek r = NactiRadek( f001 );
                if (r.F003 != RadekDne)
                    throw new JisException( "Víceřádek lze vložit jen na řádku dne" );
                string max = NactiMesic( r.F002 ).Where( x => x.F003 == RadekDne && x.F002 == r.F002 ).Max( x => x.F005 );
                Vloz( r.F002, RadekDne, LibSys.NextKey( max ) );
                tr.Commit();
            }
        }

        /// <summary>
        /// odstrani viceradek dne, hlavni radek dne nelze odstranit (VFP SPORT0_XDAY_DEL)
        /// </summary>
        public static void OdstranitViceradek(string f001)
        {
            using (var tr = SpravaDat.Transakce())
            {
                Radek r = NactiRadek( f001 );
                if (r.F003 != RadekDne || r.F005 == HlavniRadek)
                    throw new JisException( "Vybraný řádek není víceřádkem dne" );
                if (!SpravaDat.Exec( $"DELETE FROM {Smena.D0} WHERE f001 = @f001", new { f001 } ))
                    throw new JisException();
                tr.Commit();
            }
        }

        #endregion

        #region <<< pomocne >>>

        /// <summary>
        /// radek d0 v pameti - Kod / Hod / Rucne indexovane sloupcem 1..4
        /// </summary>
        class Radek
        {
            public string F001, F003, F005;
            public DateTime F002;
            public string[] Kod = new string[ Sloupcu + 1 ];
            public decimal[] Hod = new decimal[ Sloupcu + 1 ];
            public decimal[] Rucne = new decimal[ Sloupcu + 1 ];
        }

        /// <summary>
        /// zmena v bunce: kontrola typu radku, zapis a prepocet hodin osob od zmeneneho radku
        /// </summary>
        static void Zmenit(string f001, int n, string f003, Action<Radek> zapis)
        {
            if (n < 1 || n > Sloupcu)
                throw new JisException( "Vyberte sloupec 1. - 4." );

            using (var tr = SpravaDat.Transakce())
            {
                Radek r = NactiRadek( f001 );
                if (r.F003 != f003)
                    throw new JisException( f003 == CasovyRadek ? "Vybraný řádek není řádkem časového rozvrhu" : "Vybraný řádek není řádkem dne" );
                zapis( r );

                List<Radek> mesic = NactiMesic( r.F002 );
                Prepocitat( mesic, mesic.FindIndex( x => x.F001 == f001 ) );
                tr.Commit();
            }
        }

        /// <summary>
        /// prepocte hodiny osob (f00n2) od radku od - hodiny nejblizsiho predchoziho casoveho radku s vyplnenym casem,
        /// <br>prazdny sloupec radku dne = 0; zapisuje jen zmenene (VFP prochazel radky po zmene casu / osoby)</br>
        /// </summary>
        static void Prepocitat(List<Radek> mesic, int od)
        {
            decimal[] cas = new decimal[ Sloupcu + 1 ];
            for (int i = 0; i < mesic.Count; i++)
            {
                Radek r = mesic[ i ];
                for (int n = 1; n <= Sloupcu; n++)
                {
                    if (r.F003 == CasovyRadek)
                    {
                        if (!string.IsNullOrEmpty( r.Kod[ n ] ))
                            cas[ n ] = r.Hod[ n ];
                    }
                    else
                    {
                        decimal hod = string.IsNullOrEmpty( r.Kod[ n ] ) ? 0 : cas[ n ];
                        if (i >= od && hod != r.Hod[ n ])
                        {
                            Uloz( r.F001, $"f00{n}2 = @hod", new { hod } );
                            r.Hod[ n ] = hod;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// radky mesice dne datum v poradi rozpisu, zamcene do konce transakce
        /// </summary>
        static List<Radek> NactiMesic(DateTime datum)
        {
            DateTime od = new DateTime( datum.Year, datum.Month, 1 );
            return Nacti( "d0.f004 = 'R' AND d0.f002 BETWEEN @datod AND @datdo", new { datod = od, datdo = od.AddMonths( 1 ).AddDays( -1 ) } );
        }

        static Radek NactiRadek(string f001)
        {
            Radek r = Nacti( "d0.f001 = @f001", new { f001 } ).FirstOrDefault();
            if (r == null)
                throw new JisException( "Řádek rozpisu nebyl nalezen (byl mezitím odstraněn ?)\n\nObnovte rozpis" );
            return r;
        }

        static List<Radek> Nacti(string where, object parametry)
        {
            if (!SpravaDat.Select( "_d0smeny", $"SELECT d0.* FROM {Smena.D0} d0 WHERE {where} ORDER BY d0.f002, d0.f003, d0.f005 FOR UPDATE", parametry, hide: true ))
                throw new JisException();

            var radky = new List<Radek>();
            foreach (DataRow dr in Goa.TableEnum( "_d0smeny" ))
            {
                var r = new Radek
                {
                    F001 = $"{dr[ "f001" ]}".Trim(),
                    F002 = (DateTime)dr[ "f002" ],
                    F003 = $"{dr[ "f003" ]}".Trim(),
                    F005 = $"{dr[ "f005" ]}".Trim()
                };
                for (int n = 1; n <= Sloupcu; n++)
                {
                    r.Kod[ n ] = $"{dr[ $"f00{n}1" ]}".Trim();
                    r.Hod[ n ] = Convert.ToDecimal( dr[ $"f00{n}2" ] );
                    r.Rucne[ n ] = Convert.ToDecimal( dr[ $"f00{n}3" ] );
                }
                radky.Add( r );
            }
            return radky;
        }

        /// <summary>
        /// UPDATE jednoho radku d0 vcetne autora a casu zmeny - set = "f0011 = @kod, ..."
        /// </summary>
        static void Uloz(string f001, string set, object parametry = null)
        {
            var p = new Dictionary<string, object> { [ "f001" ] = f001, [ "ukey" ] = Goa.Ukey ?? "" };
            if (parametry != null)
                foreach (var prop in parametry.GetType().GetProperties())
                    p[ prop.Name ] = prop.GetValue( parametry );

            if (!SpravaDat.Exec( $"UPDATE {Smena.D0} SET {set}, f02 = @ukey, f03 = NOW() WHERE f001 = @f001", p ))
                throw new JisException();
        }

        static void Vloz(DateTime datum, string f003, string f005)
        {
            string f001 = NovyKlic();
            if (!SpravaDat.Exec( $@"
                INSERT INTO {Smena.D0} (f0, f1, f001, f002, f003, f004, f005,
                    f0011, f0012, f0013, f0021, f0022, f0023, f0031, f0032, f0033, f0041, f0042, f0043, f02, f03)
                VALUES ('', '', @f001, @f002, @f003, 'R', @f005,
                    '', 0, 0, '', 0, 0, '', 0, 0, '', 0, 0, @ukey, NOW())",
                new { f001, f002 = datum.Date, f003, f005, ukey = Goa.Ukey ?? "" } ))
                throw new JisException();
        }

        /// <summary>
        /// novy klic F001 z rady DRA / DAA (VFP _XGENKEY 'F001','dra' / 'daa')
        /// </summary>
        static string NovyKlic()
        {
            string klic = LibSys.GenKey( Smena.DA, "F001" );
            if (string.IsNullOrWhiteSpace( klic ))
                throw new JisException( "Chyba při generování klíče F001 - nelze uložit !" );
            return klic;
        }

        #endregion
    }
}
