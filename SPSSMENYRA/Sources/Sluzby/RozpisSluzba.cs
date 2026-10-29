using JisResources;
using JisResources.Sources;
using System;

namespace SPSSMENYRA
{
    /// <summary>
    /// Rozpis sluzeb (tabulka d0 - dr0 / da0) - bez dialogu, zapisy v transakci.
    /// <br>Pri chybe vyhodi JisException: s textem = upozorneni pro uzivatele, bez textu = chyba uz byla ohlasena (databaze).</br>
    /// <br>Radky d0: f003 = '1' casovy radek (f00n1 = kod t2, f00n2 = hodiny t2), f003 = '2' radek dne (f00n1 = kod t3,
    /// f00n2 = hodiny z casoveho radku, f00n3 = rucne opravene hodiny), f005 = '00' hlavni radek dne, '01'.. viceradek.</br>
    /// </summary>
    public static class RozpisSluzba
    {
        public const string CasovyRadek = "1";
        public const string RadekDne = "2";
        public const string HlavniRadek = "00";

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
    }
}
