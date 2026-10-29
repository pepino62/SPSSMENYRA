using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SPSSMENYRA
{
    /// <summary>
    /// Rozpis sluzeb na mesic (VFP SPORT0 / SPORTA - zalozka ROZPIS, gridy SPORT0_GRD001 rozpis a SPORT0_GRD002 soucty pracovniku)
    /// <br>dvojklik ve sloupci 1. - 4.: casovy radek = vyber casu, radek dne = vyber pracovnika; ve sloupci hod. = rucni oprava hodin</br>
    /// <br>prave tlacitko: vymazat osobu / cas, vlozit / odstranit casovy radek a viceradek dne (VFP MENUS SPORT0_11 - 14)</br>
    /// </summary>
    public partial class Rozpis : JisUserControl
    {
        RozpisBind pageBase;

        public Rozpis()
        {
            pageBase = new RozpisBind();
            InitializeComponent();
            DataContext = pageBase;
            pageBase.Stranka = this;
            pageBase.SetDataGrid( this, pageBase );
            pageBase.ReadControls( this );
            USERDATAGRID1.dynamicColumn = new DynamicColumnSet();   // barvy radku (casovy radek, vikend, rucne opravene hodiny)
            USERDATAGRID2.dynamicColumn = new DynamicColumnSet();   // radek Celkem
        }

        public override void JisRefresh_Page()
        {
            if (Goa.IsInit( Name ) && pageBase.Status == EStatus.fill)
                JisReloaded_Page();
            else if (pageBase.Status == EStatus.view)
                USERDATAGRID1.SetRowCol();
        }

        public override void JisUnloaded_Page()
        {
        }

        public override void JisReloaded_Page(bool _resetColums = false)
        {
            try
            {
                NabidnoutGenerovani();
                using (WaitCursor waitCursor = new WaitCursor())
                {
                    if (!pageBase.DataSource())        // naplni zdroj dat
                        throw new JisException();
                    USERDATAGRID1.RestoreGrid( resetColumns: _resetColums );        // sestavi grid - pripoji zdroj dat, nastavi columns, sort, refresh ...
                    USERDATAGRID2.RestoreGrid( resetColumns: _resetColums );
                    pageBase.SoucetDne();
                    pageBase.SetStatus( EStatus.view );
                }
            }
            catch (JisException)
            {
                pageBase.SetStatus( EStatus.start );
            }
            catch (Exception ex)
            {
                Msg.Err( ex );
            }
        }

        public override void JisRefresh_Data()
        {
            Refresh();
        }


        public void Refresh()
        {
            if (pageBase.Status != EStatus.view)
                JisReloaded_Page( true );
            else
            {
                NabidnoutGenerovani();
                using (new WaitCursor())
                {
                    if (pageBase.DataSource())       // naplni zdroj dat
                    {
                        USERDATAGRID1.RefreshSourceData( false );  // false - bez znovunaplneni dat
                        USERDATAGRID2.RefreshSourceData( false );
                        pageBase.SoucetDne();
                    }
                }
            }
        }

        /// <summary>
        /// pro mesic bez zaznamu nabidne vygenerovani rozpisu - jen jednou pro kazdy vybrany mesic (VFP SPORT0_XMONTH_VALID)
        /// </summary>
        void NabidnoutGenerovani()
        {
            string mesic = $"{pageBase.Mesic:00}/{pageBase.Rok}";
            if (pageBase.NabidnutyMesic == mesic)
                return;
            pageBase.NabidnutyMesic = mesic;

            try
            {
                if (!RozpisSluzba.ExistujeMesic( pageBase.Rok, pageBase.Mesic )
                    && Msg.YesNo( $"Nenalezeny žádné záznamy k tomuto měsíci ( {mesic} )\n\nVygenerovat rozpis pro tento měsíc ?" ))
                {
                    using (new WaitCursor())
                        RozpisSluzba.GenerovatMesic( pageBase.Rok, pageBase.Mesic );
                }
            }
            catch (JisException e1)
            {
                if (!string.IsNullOrEmpty( e1.Text ))
                    Msg.Ok( e1.Text );
            }
            catch (Exception ex)
            {
                Msg.Err( ex );
            }
        }

        private void CboMesic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                Refresh();
        }

        private void BtnObnovit_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }


        private class RozpisBind : DataBindBasic
        {
            public Rozpis Stranka { get; set; }

            int _Rok = DateTime.Today.Year;
            int _Mesic = DateTime.Today.Month;
            public int Rok { get => _Rok; set => SetField( ref _Rok, value ); }
            public int Mesic { get => _Mesic; set => SetField( ref _Mesic, value ); }

            /// <summary>
            /// roky 2016 .. pristi rok (VFP spinner mel natvrdo 2016 - 2026)
            /// </summary>
            public List<int> Roky { get; } = new List<int>();
            public List<MesicPolozka> Mesice { get; } = new List<MesicPolozka>();

            /// <summary>
            /// mesic, pro ktery uz bylo nabidnuto generovani (mm/rrrr)
            /// </summary>
            public string NabidnutyMesic { get; set; } = "";

            /// <summary>
            /// pracovnici oznaceni v pravem gridu (ft301) - rozpis ukazuje jen je (VFP SPORT0_XOS_MARK, tmq.ft301)
            /// </summary>
            HashSet<string> oznaceni = new HashSet<string>();
            bool filtrCeka;

            string _FiltrText = "";
            public string FiltrText { get => _FiltrText; set => SetField( ref _FiltrText, value ); }

            public RozpisBind() : base()
            {
                AddMethod( () => { SaveParameters(); }, "CallCloseMethod" );

                for (int i = DateTime.Today.Year + 1; i >= 2016; i--)
                    Roky.Add( i );
                var cz = CultureInfo.GetCultureInfo( "cs-CZ" );
                for (int i = 1; i <= 12; i++)
                    Mesice.Add( new MesicPolozka { Cislo = i, Nazev = cz.TextInfo.ToTitleCase( cz.DateTimeFormat.MonthNames[ i - 1 ] ) } );

                Init();
            }

            public void Init()
            {
                ParamClear();
                ParamAdd( () => Rok, DateTime.Today.Year );
                ParamAdd( () => Mesic, DateTime.Today.Month );
                ReadParameters();
                if (!Roky.Contains( Rok ))
                    Rok = DateTime.Today.Year;
                if (Mesic < 1 || Mesic > 12)
                    Mesic = DateTime.Today.Month;
            }

            public bool DataSource()
            {
                try
                {
                    if (Rozpis())
                        Pracovnici();
                }
                catch (Exception ex)
                {
                    JisLog.Chyba( ex );
                }
                finally { }

                return (proCmd.Stav == 0);
            }

            /// <summary>
            /// rozpis mesice (VFP SPORT0_2TMP_C)
            /// <br>FX1..4 - casovy radek: rozmezi z t2, radek dne: jmeno z t3; FK1..4 - kod (f00n1)</br>
            /// <br>FH1..4 - hodiny: casovy radek f00n2, radek dne f00n3 (rucne), jinak f00n2; FR1..4 = 1 rucne opravene hodiny</br>
            /// </summary>
            bool Rozpis()
            {
                DateTime od = new DateTime( Rok, Mesic, 1 );
                string sloupce = "";
                string join = "";
                for (int n = 1; n <= RozpisSluzba.Sloupcu; n++)
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

                string cmd = $@"
                    SELECT d0.f001, d0.f002, d0.f003, d0.f005,
                        IF(d0.f003 = '2' AND d0.f005 = '00', DAY(d0.f002), NULL) AS fday,
                        IF(d0.f003 = '2', ELT(DAYOFWEEK(d0.f002), 'Ne', 'Po', 'Út', 'St', 'Čt', 'Pá', 'So'), '') AS fdayc,
                        DAYOFWEEK(d0.f002) AS fdayn{sloupce}
                    FROM {Smena.D0} d0{join}
                    WHERE d0.f004 = 'R' AND d0.f002 BETWEEN @datod AND @datdo
                    ORDER BY d0.f002, d0.f003, d0.f005";

                if (!SpravaDat.Select( "rozpis", cmd, new { datod = od, datdo = od.AddMonths( 1 ).AddDays( -1 ) } ))
                    return false;
                Filtrovat();
                return true;
            }

            /// <summary>
            /// filtr podle oznacenych pracovniku - u ostatnich se v radcich dne skryje jmeno a hodiny (VFP SPORT0_2TMP_C s tmq.ft301)
            /// <br>FK (kod) a FS (hodiny pro soucet dne) zustavaji - editace i soucty pracuji se skutecnymi daty</br>
            /// </summary>
            void Filtrovat()
            {
                if (oznaceni.Count == 0)
                    return;
                DataTable dt = Goa.Table( "rozpis" );
                foreach (DataColumn c in dt.Columns)
                    c.ReadOnly = false;
                foreach (DataRow r in dt.Rows)
                {
                    if ($"{r[ "f003" ]}" != RozpisSluzba.RadekDne)
                        continue;
                    for (int n = 1; n <= RozpisSluzba.Sloupcu; n++)
                    {
                        string kod = $"{r[ $"fk{n}" ]}".Trim();
                        if (kod != "" && !oznaceni.Contains( kod ))
                        {
                            r[ $"fx{n}" ] = "";
                            r[ $"fh{n}" ] = DBNull.Value;
                            r[ $"fr{n}" ] = 0;
                        }
                    }
                }
                dt.AcceptChanges();
            }

            /// <summary>
            /// zmena oznaceni v pravem gridu - po dokonceni kliknuti (i hromadneho oznac vse / odznac vse) jednou obnovi rozpis
            /// </summary>
            void Pracovnici_ColumnChanged(object sender, DataColumnChangeEventArgs e)
            {
                if (!string.Equals( e.Column.ColumnName, "fmark", StringComparison.OrdinalIgnoreCase ) || filtrCeka)
                    return;
                filtrCeka = true;
                Stranka.Dispatcher.BeginInvoke( new Action( () =>
                {
                    filtrCeka = false;
                    DataTable prac = Goa.Table( "pracovnici" );
                    oznaceni = new HashSet<string>( prac.AsEnumerable()
                        .Where( r => $"{r[ "ftyp" ]}" == "1" && Convert.ToInt32( r[ "fmark" ] ) != 0 )
                        .Select( r => $"{r[ "ft301" ]}".Trim() ) );
                    FiltrText = oznaceni.Count == 0 ? "" : "rozpis jen pro : " + string.Join( ", ", prac.AsEnumerable()
                        .Where( r => oznaceni.Contains( $"{r[ "ft301" ]}".Trim() ) ).Select( r => $"{r[ "ft302" ]}".Trim() ) );
                    Stranka.Refresh();
                } ), System.Windows.Threading.DispatcherPriority.Background );
            }

            /// <summary>
            /// aktivni pracovnici s hodinami a smenami za mesic a hodinami za rok + radek Celkem (VFP SPORT0_2TMT3, SPORT0_2TMT3_SUM)
            /// <br>FHODX - hodiny dne vybraneho v rozpisu, dopocitava SoucetDne</br>
            /// </summary>
            bool Pracovnici()
            {
                DateTime od = new DateTime( Rok, Mesic, 1 );
                string cmd = $@"
                    SELECT FALSE AS fmark, t3.ft301, t3.ft302, t3.ft304, '1' AS ftyp,
                        CAST(0 AS DECIMAL(6,1)) AS fhodx,
                        CAST(IFNULL(m.hod, 0) AS DECIMAL(7,1)) AS fhodm,
                        CAST(IFNULL(m.smen, 0) AS DECIMAL(4,0)) AS fsmen,
                        CAST(IFNULL(r.hod, 0) AS DECIMAL(7,1)) AS fhodc
                    FROM {Smena.T3} t3
                    LEFT JOIN (SELECT x.ft301, SUM(x.hod) AS hod, SUM(x.smen) AS smen FROM ({Hodiny( "@mesod", "@mesdo" )}) x GROUP BY x.ft301) m ON m.ft301 = t3.ft301
                    LEFT JOIN (SELECT x.ft301, SUM(x.hod) AS hod FROM ({Hodiny( "@rokod", "@rokdo" )}) x GROUP BY x.ft301) r ON r.ft301 = t3.ft301
                    WHERE t3.f0 = ''
                    ORDER BY t3.ft302";

                if (!SpravaDat.Select( "pracovnici", cmd, new
                {
                    mesod = od,
                    mesdo = od.AddMonths( 1 ).AddDays( -1 ),
                    rokod = new DateTime( Rok, 1, 1 ),
                    rokdo = new DateTime( Rok, 12, 31 )
                } ))
                    return false;

                DataTable dt = Goa.Table( "pracovnici" );
                dt.ColumnChanged -= Pracovnici_ColumnChanged;
                foreach (DataColumn c in dt.Columns)
                    c.ReadOnly = false;

                // oznaceni zustava i po obnoveni / zmene mesice - jen u pracovniku, kteri jsou v seznamu
                oznaceni.IntersectWith( dt.AsEnumerable().Select( r => $"{r[ "ft301" ]}".Trim() ) );
                foreach (DataRow r in dt.Rows)
                    if (oznaceni.Contains( $"{r[ "ft301" ]}".Trim() ))
                        r[ "fmark" ] = 1;
                if (oznaceni.Count == 0)
                    FiltrText = "";

                DataRow celkem = dt.NewRow();
                celkem[ "ft301" ] = "";
                celkem[ "ft302" ] = "Celkem";
                celkem[ "ft304" ] = 0;
                celkem[ "ftyp" ] = "2";
                foreach (string f in new[] { "fhodx", "fhodm", "fsmen", "fhodc" })
                    celkem[ f ] = dt.AsEnumerable().Sum( r => Convert.ToDecimal( r[ f ] ) );
                celkem[ "fmark" ] = 0;
                dt.Rows.Add( celkem );
                dt.AcceptChanges();
                dt.ColumnChanged += Pracovnici_ColumnChanged;
                return true;
            }

            /// <summary>
            /// hodiny a smeny pracovniku ze vsech 4 sloupcu radku dne v obdobi (rucne opravene maji prednost)
            /// </summary>
            static string Hodiny(string od, string @do)
                => string.Join( " UNION ALL ", Enumerable.Range( 1, RozpisSluzba.Sloupcu ).Select( n => $@"
                    SELECT d0.f00{n}1 AS ft301, IF(d0.f00{n}3 <> 0, d0.f00{n}3, d0.f00{n}2) AS hod, 1 AS smen
                    FROM {Smena.D0} d0
                    WHERE d0.f003 = '2' AND d0.f004 = 'R' AND d0.f00{n}1 <> '' AND d0.f002 BETWEEN {od} AND {@do}" ) );

            /// <summary>
            /// hodiny pracovniku za den vybrany v rozpisu (VFP SPORT0_2TMT3_SUM_DAY) - casovy radek = bez hodin, filtr se neuplatni
            /// </summary>
            public void SoucetDne()
            {
                DataTable rozpis = Goa.Table( "rozpis" );
                DataTable prac = Goa.Table( "pracovnici" );
                if (rozpis == null || prac == null)
                    return;

                DataRow vybrany = Stranka.USERDATAGRID1.GetRow();
                DateTime? den = vybrany != null && $"{vybrany[ "f003" ]}" == RozpisSluzba.RadekDne ? (DateTime?)vybrany[ "f002" ] : null;

                var hodiny = new Dictionary<string, decimal>();
                if (den != null)
                    foreach (DataRow r in rozpis.AsEnumerable().Where( r => $"{r[ "f003" ]}" == RozpisSluzba.RadekDne && (DateTime)r[ "f002" ] == den ))
                        for (int n = 1; n <= RozpisSluzba.Sloupcu; n++)
                        {
                            string kod = $"{r[ $"fk{n}" ]}".Trim();
                            if (kod != "" && r[ $"fs{n}" ] != DBNull.Value)    // fs - i pracovnici skryti filtrem
                                hodiny[ kod ] = (hodiny.TryGetValue( kod, out decimal h ) ? h : 0) + Convert.ToDecimal( r[ $"fs{n}" ] );
                        }

                foreach (DataRow p in prac.Rows)
                {
                    decimal h = $"{p[ "ftyp" ]}" == "2" ? hodiny.Values.Sum() : (hodiny.TryGetValue( $"{p[ "ft301" ]}".Trim(), out decimal x ) ? x : 0);
                    if (Convert.ToDecimal( p[ "fhodx" ] ) != h)
                        p[ "fhodx" ] = h;
                }
            }

            public override int ChangeRow(object _object, ModifierKeys modifierKeys, DataBindBasic _baseCodeBind)
            {
                if (_object == Stranka?.USERDATAGRID1)
                    SoucetDne();
                return 0;
            }

            #region <<< editace rozpisu >>>

            /// <summary>
            /// dvojklik v rozpisu
            /// </summary>
            public override int ExecuteCommand(object _object, ModifierKeys modifierKeys, DataBindBasic _baseCodeBind)
            {
                if (_object != Stranka?.USERDATAGRID1)
                    return base.ExecuteCommand( _object, modifierKeys, _baseCodeBind );

                DataRow row = Stranka.USERDATAGRID1.GetRow();
                (string typ, int n) = Sloupec();
                if (row == null || n == 0)
                    return 1;

                string f001 = $"{row[ "f001" ]}".Trim();
                bool casovy = $"{row[ "f003" ]}" == RozpisSluzba.CasovyRadek;

                if (typ == "X" && casovy)
                    Nabidka( $"SELECT t2.ft201 AS fkod, CONCAT(t2.ft202, '   ', FORMAT(t2.ft203, 1)) AS fnazev FROM {Smena.T2} t2 WHERE t2.f0 = '' ORDER BY t2.ft204",
                        kod => RozpisSluzba.NastavitCas( f001, n, kod ) );
                else if (typ == "X")
                    Nabidka( $"SELECT t3.ft301 AS fkod, t3.ft302 AS fnazev FROM {Smena.T3} t3 WHERE t3.f0 = '' ORDER BY t3.ft302",
                        kod => RozpisSluzba.NastavitOsobu( f001, n, kod ) );
                else if (!casovy && $"{row[ $"fk{n}" ]}".Trim() != "")
                {
                    decimal hod = row[ $"fh{n}" ] == DBNull.Value ? 0 : Convert.ToDecimal( row[ $"fh{n}" ] );
                    decimal? nove = new HodinyDialog().ShowDialog( Window.GetWindow( Stranka ),
                        $"{row[ $"fx{n}" ]}".Trim() + $",  {(DateTime)row[ "f002" ]:d.M.yyyy}", hod );
                    if (nove != null)
                        Proved( () => RozpisSluzba.NastavitHodiny( f001, n, nove.Value ) );
                }
                return 1;
            }

            /// <summary>
            /// kontextove menu rozpisu - polozky podle radku a sloupce (VFP MENUS SPORT0_11 - 14, podminky fcondition)
            /// </summary>
            public override void GridContextMenu(JisDataGrid grid, ContextMenu menu)
            {
                if (grid != Stranka?.USERDATAGRID1)
                    return;
                DataRow row = grid.GetRow();
                if (row == null)
                    return;

                string f001 = $"{row[ "f001" ]}".Trim();
                bool casovy = $"{row[ "f003" ]}" == RozpisSluzba.CasovyRadek;
                bool hlavni = $"{row[ "f005" ]}".Trim() == RozpisSluzba.HlavniRadek;
                int n = Sloupec().n;
                bool vyplneno = n > 0 && $"{row[ $"fk{n}" ]}".Trim() != "";

                var polozky = new List<JisMenuItem>();
                if (!casovy && vyplneno)
                    polozky.Add( Polozka( $"Vymazat osobu ({n}.)", () => RozpisSluzba.SmazatOsobu( f001, n ) ) );
                if (casovy && vyplneno)
                    polozky.Add( Polozka( $"Vymazat čas ({n}.)", () => RozpisSluzba.SmazatCas( f001, n ) ) );
                if (casovy)
                    polozky.Add( Polozka( "Odstranit řádek časového rozvrhu", () => RozpisSluzba.OdstranitCasovyRadek( f001 ) ) );
                if (!casovy && hlavni)
                    polozky.Add( Polozka( "Vložit řádek časového rozvrhu", () => RozpisSluzba.VlozitCasovyRadek( f001 ) ) );
                if (!casovy && !hlavni)
                    polozky.Add( Polozka( "Odstranit víceřádek dne", () => RozpisSluzba.OdstranitViceradek( f001 ) ) );
                if (!casovy)
                    polozky.Add( Polozka( "Vložit víceřádek dne", () => RozpisSluzba.VlozitViceradek( f001 ) ) );

                if (polozky.Count == 0)
                    return;
                menu.Items.Insert( 0, new Separator() { Background = new SolidColorBrush( Colors.Black ) } );
                for (int i = polozky.Count - 1; i >= 0; i--)
                    menu.Items.Insert( 0, polozky[ i ] );
            }

            /// <summary>
            /// aktivni sloupec rozpisu: FX1..4 = "X", FH1..4 = "H" + cislo sloupce, jinak n = 0
            /// </summary>
            (string typ, int n) Sloupec()
            {
                string source = (Stranka.USERDATAGRID1.gridBase?.currGrd?.ActiveSource ?? "").Trim().ToUpper();
                Match m = Regex.Match( source, @"^F([XH])([1-4])$" );
                return m.Success ? (m.Groups[ 1 ].Value, int.Parse( m.Groups[ 2 ].Value )) : ("", 0);
            }

            /// <summary>
            /// nabidka casu / pracovniku u mysi (VFP combo ve sloupci), vyber provede akci
            /// </summary>
            void Nabidka(string sql, Action<string> vybrano)
            {
                if (!SpravaDat.Select( "_smenynab", sql, hide: true ))
                    return;
                var menu = new ContextMenu { Placement = PlacementMode.MousePoint, PlacementTarget = Stranka.USERDATAGRID1 };
                foreach (DataRow r in Goa.TableEnum( "_smenynab" ).ToList())
                {
                    string kod = $"{r[ "fkod" ]}".Trim();
                    menu.Items.Add( Polozka( $"{r[ "fnazev" ]}".Trim(), () => vybrano( kod ) ) );
                }
                menu.IsOpen = true;
            }

            JisMenuItem Polozka(string text, Action akce)
            {
                var mi = new JisMenuItem() { Header = text, FontFamily = new FontFamily( "Segoe UI" ), FontSize = 12 };
                mi.Click += (s, e) => Proved( akce );
                return mi;
            }

            /// <summary>
            /// provede zmenu rozpisu a obnovi stranku
            /// </summary>
            void Proved(Action akce)
            {
                try
                {
                    using (new WaitCursor())
                        akce();
                }
                catch (JisException e1)
                {
                    if (!string.IsNullOrEmpty( e1.Text ))
                        Msg.Ok( e1.Text );
                }
                catch (Exception ex)
                {
                    Msg.Err( ex );
                }
                Stranka.Refresh();
            }

            #endregion
        }

        private class MesicPolozka
        {
            public int Cislo { get; set; }
            public string Nazev { get; set; }
        }
    }
}
