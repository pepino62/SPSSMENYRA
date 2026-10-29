using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace SPSSMENYRA
{
    /// <summary>
    /// Tisky rozpisu sluzeb - zobrazeni pres JisBReportViewer (export do PDF z nahledu)
    /// <br>SmenyRozpis.rdlc - rozpis mesice + soucty pracovniku na dalsi strane, pro vice mesicu kazdy mesic od nove strany (VFP RES02, XRES01)</br>
    /// <br>SmenyPrehled.rdlc - prehled hodin pracovniku po mesicich za rok (VFP RES01)</br>
    /// </summary>
    public static class SmenyTisk
    {
        public const string RozpisReport = "RozpisTisk";
        public const string PrehledReport = "PrehledTisk";

        /// <summary>
        /// rozpis mesice (VFP RES02)
        /// </summary>
        public static void Rozpis(int rok, int mesic) => Rozpis( rok, new[] { mesic } );

        /// <summary>
        /// rozpis vsech mesicu roku, ktere maji zaznamy (VFP XRES01 - tiskl u vsech mesicu rozpis prvniho mesice, opraveno)
        /// </summary>
        public static void RozpisRok(int rok)
        {
            if (!SpravaDat.Select( "_tiskmesice", $"SELECT DISTINCT MONTH(d0.f002) AS fmesic FROM {Smena.D0} d0 WHERE d0.f004 = 'R' AND YEAR(d0.f002) = @rok ORDER BY 1",
                new { rok }, hide: true ))
                throw new JisException();
            var mesice = Goa.TableEnum( "_tiskmesice" ).Select( r => Convert.ToInt32( r[ "fmesic" ] ) ).ToList();
            if (mesice.Count == 0)
                throw new JisException( $"V roce {rok} není žádný rozpis služeb" );
            Rozpis( rok, mesice );
        }

        static void Rozpis(int rok, IEnumerable<int> mesice)
        {
            DataTable dt = NovaRozpisTabulka();
            foreach (int mesic in mesice)
                RozpisMesice( dt, rok, mesic );

            var report = new JisBReportViewer();
            report.AddDataSources( RozpisReport, dt );
            report.ShowDialog( "SmenyRozpis.rdlc" );
        }

        /// <summary>
        /// prehled hodin pracovniku po mesicich za rok (VFP RES01) - data ze zalozky SUMARIZACE bez radku Celkem (soucet dela sestava)
        /// </summary>
        public static void Prehled(int rok, DataTable sumarizace)
        {
            DataTable dt = new DataTable( PrehledReport );
            dt.Columns.Add( "FTITUL", typeof( string ) );
            dt.Columns.Add( "FJMENO", typeof( string ) );
            foreach (string f in new[] { "FUVAZ", "FDOVP", "FDOVV", "FROK" }.Concat( Enumerable.Range( 1, 12 ).Select( m => $"F{m:00}" ) ))
                dt.Columns.Add( f, typeof( decimal ) );

            string titul = $"Přehled služeb - {Smena.Titul} Služby : {rok}";
            foreach (DataRow r in sumarizace.AsEnumerable().Where( r => $"{r[ "ftyp" ]}" == "1" ))
            {
                DataRow n = dt.NewRow();
                n[ "FTITUL" ] = titul;
                n[ "FJMENO" ] = $"{r[ "ft302" ]}".Trim();
                n[ "FUVAZ" ] = Cislo( r[ "ft304" ] );
                n[ "FDOVP" ] = Cislo( r[ "fdovp" ] );
                n[ "FDOVV" ] = Cislo( r[ "fdovv" ] );
                n[ "FROK" ] = Cislo( r[ "frok" ] );
                for (int m = 1; m <= 12; m++)
                    n[ $"F{m:00}" ] = Cislo( r[ $"f{m:00}" ] );
                dt.Rows.Add( n );
            }
            if (dt.Rows.Count == 0)
                throw new JisException( $"V roce {rok} nejsou žádné služby" );

            var report = new JisBReportViewer();
            report.AddDataSources( PrehledReport, dt );
            report.ShowDialog( "SmenyPrehled.rdlc" );
        }

        /// <summary>
        /// datova tabulka sestavy rozpisu - FTYP 'R' radek rozpisu, 'S' soucet pracovnika; FMESIC skupina (nova strana)
        /// </summary>
        static DataTable NovaRozpisTabulka()
        {
            DataTable dt = new DataTable( RozpisReport );
            dt.Columns.Add( "FMESIC", typeof( int ) );
            dt.Columns.Add( "FTITUL", typeof( string ) );
            dt.Columns.Add( "FTYP", typeof( string ) );
            dt.Columns.Add( "FDEN", typeof( string ) );
            dt.Columns.Add( "FDENC", typeof( string ) );
            dt.Columns.Add( "FCAS", typeof( bool ) );
            for (int n = 1; n <= RozpisSluzba.Sloupcu; n++)
            {
                dt.Columns.Add( $"FX{n}", typeof( string ) );
                dt.Columns.Add( $"FH{n}", typeof( decimal ) );
            }
            dt.Columns.Add( "FJMENO", typeof( string ) );
            foreach (string f in new[] { "FSMEN", "FHODM", "FHODR", "FDOVM", "FDOVR", "FDOVP", "FUVAZ" })
                dt.Columns.Add( f, typeof( decimal ) );
            return dt;
        }

        static void RozpisMesice(DataTable dt, int rok, int mesic)
        {
            DateTime od = new DateTime( rok, mesic, 1 );
            DateTime @do = od.AddMonths( 1 ).AddDays( -1 );
            string titul = $"Rozpis služeb - {Smena.Titul} Služby : {mesic:00} / {rok}";

            if (!SpravaDat.Select( "_tiskrozpis", RozpisSluzba.RozpisSql(), new { datod = od, datdo = @do }, hide: true ))
                throw new JisException();
            foreach (DataRow r in Goa.TableEnum( "_tiskrozpis" ).ToList())
            {
                DataRow n = dt.NewRow();
                n[ "FMESIC" ] = mesic;
                n[ "FTITUL" ] = titul;
                n[ "FTYP" ] = "R";
                bool cas = $"{r[ "f003" ]}" == RozpisSluzba.CasovyRadek;
                n[ "FCAS" ] = cas;
                n[ "FDEN" ] = r[ "fday" ] == DBNull.Value ? "" : $"{r[ "fday" ]}.";
                n[ "FDENC" ] = $"{r[ "fdayc" ]}";
                for (int i = 1; i <= RozpisSluzba.Sloupcu; i++)
                {
                    n[ $"FX{i}" ] = $"{r[ $"fx{i}" ]}".Trim();
                    n[ $"FH{i}" ] = Cislo( r[ $"fh{i}" ] );
                }
                dt.Rows.Add( n );
            }

            // soucty aktivnich pracovniku - smeny a hodiny za mesic, hodiny za rok, dovolena (VFP RES02 strana 2)
            string cmd = $@"
                SELECT t3.ft302, t3.ft304, m.smen, m.hod AS hodm, r.hod AS hodr, dm.dov AS dovm, dr.dov AS dovr, dp.dov AS dovp
                FROM {Smena.T3} t3
                LEFT JOIN (SELECT x.ft301, SUM(x.hod) AS hod, SUM(x.smen) AS smen FROM ({RozpisSluzba.HodinySql( "@mesod", "@mesdo" )}) x GROUP BY x.ft301) m ON m.ft301 = t3.ft301
                LEFT JOIN (SELECT x.ft301, SUM(x.hod) AS hod FROM ({RozpisSluzba.HodinySql( "@rokod", "@rokdo" )}) x GROUP BY x.ft301) r ON r.ft301 = t3.ft301
                LEFT JOIN (SELECT d1.ft301, SUM(d1.f103) AS dov FROM {Smena.D1} d1 WHERE d1.f102 = '2' AND d1.f104 BETWEEN @mesod AND @mesdo GROUP BY d1.ft301) dm ON dm.ft301 = t3.ft301
                LEFT JOIN (SELECT d1.ft301, SUM(d1.f103) AS dov FROM {Smena.D1} d1 WHERE d1.f102 = '2' AND YEAR(d1.f104) = @rok GROUP BY d1.ft301) dr ON dr.ft301 = t3.ft301
                LEFT JOIN (SELECT d1.ft301, SUM(d1.f103) AS dov FROM {Smena.D1} d1 WHERE d1.f102 = '1' AND YEAR(d1.f104) = @rok GROUP BY d1.ft301) dp ON dp.ft301 = t3.ft301
                WHERE t3.f0 = ''
                ORDER BY t3.ft302";
            if (!SpravaDat.Select( "_tisksoucty", cmd, new
            {
                rok,
                mesod = od,
                mesdo = @do,
                rokod = new DateTime( rok, 1, 1 ),
                rokdo = new DateTime( rok, 12, 31 )
            }, hide: true ))
                throw new JisException();
            foreach (DataRow r in Goa.TableEnum( "_tisksoucty" ).ToList())
            {
                DataRow n = dt.NewRow();
                n[ "FMESIC" ] = mesic;
                n[ "FTITUL" ] = titul;
                n[ "FTYP" ] = "S";
                n[ "FCAS" ] = false;
                n[ "FJMENO" ] = $"{r[ "ft302" ]}".Trim();
                n[ "FSMEN" ] = Cislo( r[ "smen" ] );
                n[ "FHODM" ] = Cislo( r[ "hodm" ] );
                n[ "FHODR" ] = Cislo( r[ "hodr" ] );
                n[ "FDOVM" ] = Cislo( r[ "dovm" ] );
                n[ "FDOVR" ] = Cislo( r[ "dovr" ] );
                n[ "FDOVP" ] = Cislo( r[ "dovp" ] );
                n[ "FUVAZ" ] = Cislo( r[ "ft304" ] );
                dt.Rows.Add( n );
            }
        }

        /// <summary>
        /// cislo do sestavy - 0 a prazdne jako prazdna bunka (jako VFP)
        /// </summary>
        static object Cislo(object x)
            => x == null || x == DBNull.Value || Convert.ToDecimal( x ) == 0 ? (object)DBNull.Value : Convert.ToDecimal( x );
    }
}
