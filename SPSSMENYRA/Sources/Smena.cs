using System;
using System.Linq;

namespace SPSSMENYRA
{
    /// <summary>
    /// Varianta programu - R.A. (VFP SPORTA, tabulky dr*) nebo ATK (VFP SPORT0, tabulky da*), logika je shodna.
    /// <br>Nastavit pri startu (Smena.Init), v SQL pouzivat alias, aby zbytek dotazu byl pro obe varianty stejny:</br>
    /// <code>
    /// SpravaDat.Select( "rozpis", $"SELECT d0.* FROM {Smena.D0} d0 WHERE d0.f002 = @datum", new { datum } );
    /// </code>
    /// </summary>
    public static class Smena
    {
        public const string RA = "RA";
        public const string ATK = "ATK";

        static string _varianta;

        /// <summary>
        /// varianta z parametru prikazove radky (RA / ATK), bez parametru R.A.
        /// <br>vraci args bez parametru varianty - zbytek zpracuje ProjectBind (externi volani)</br>
        /// </summary>
        public static string[] Init(string[] args)
        {
            string varianta = args.FirstOrDefault( a => JeVarianta( a ) );
            Init( varianta == null ? RA : varianta.TrimStart( '/', '-' ).ToUpper() );
            return args.Where( a => !JeVarianta( a ) ).ToArray();
        }

        public static void Init(string varianta)
        {
            if (varianta != RA && varianta != ATK)
                throw new ArgumentException( $"Neznámá varianta programu '{varianta}' (RA nebo ATK)", nameof( varianta ) );
            _varianta = varianta;
        }

        static bool JeVarianta(string arg)
        {
            string a = (arg ?? "").TrimStart( '/', '-' ).ToUpper();
            return a == RA || a == ATK;
        }

        static string Varianta => _varianta ?? throw new InvalidOperationException( "Smena.Init nebyl zavolán" );

        public static bool JeRA => Varianta == RA;

        static string Nazev(string cislo) => (JeRA ? "dr" : "da") + cislo;

        public static string D0 => Nazev( "0" );    // rozpis sluzeb
        public static string D1 => Nazev( "1" );    // dovolena
        public static string DA => Nazev( "a" );    // klice (DRA / DAA)
        public static string T2 => JeRA ? "tr2" : "ta2";    // casova rozmezi
        public static string T3 => JeRA ? "tr3" : "ta3";    // pracovnici

        /// <summary>
        /// nazev do titulku okna
        /// </summary>
        public static string Titul => JeRA ? "R.A." : "ATK";

        /// <summary>
        /// role pro vstup (VFP SPORTX - tlacitko R.A. SMĚNY: xIsRole('002,100'), ATK SMĚNY: xIsRole('003,100'))
        /// </summary>
        public static string Role => JeRA ? "002,100" : "003,100";

        /// <summary>
        /// parametr tisku (VFP RES01 / RES02: 'R' R.A., 'A' ATK)
        /// </summary>
        public static string Tisk => JeRA ? "R" : "A";
    }
}
