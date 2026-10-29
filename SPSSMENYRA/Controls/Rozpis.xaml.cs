using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SPSSMENYRA
{
    /// <summary>
    /// Rozpis sluzeb na mesic (VFP SPORT0 / SPORTA - zalozka ROZPIS, grid SPORT0_GRD001)
    /// </summary>
    public partial class Rozpis : JisUserControl
    {
        RozpisBind pageBase;

        public Rozpis()
        {
            pageBase = new RozpisBind();
            InitializeComponent();
            DataContext = pageBase;
            pageBase.SetDataGrid( this, pageBase );
            pageBase.ReadControls( this );
            USERDATAGRID1.dynamicColumn = new DynamicColumnSet();   // barvy radku (casovy radek, vikend, rucne opravene hodiny)
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
                        USERDATAGRID1.RefreshSourceData( false );  // false - bez znovunaplneni dat
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

            /// <summary>
            /// rozpis mesice (VFP SPORT0_2TMP_C)
            /// <br>FX1..4 - casovy radek: rozmezi z t2, radek dne: jmeno z t3</br>
            /// <br>FH1..4 - hodiny: casovy radek f00n2, radek dne f00n3 (rucne), jinak f00n2; FR1..4 = 1 rucne opravene hodiny</br>
            /// </summary>
            public bool DataSource()
            {
                try
                {
                    DateTime od = new DateTime( Rok, Mesic, 1 );
                    string sloupce = "";
                    string join = "";
                    for (int n = 1; n <= 4; n++)
                    {
                        sloupce += $@",
                            IF(d0.f003 = '1', IFNULL(t2_{n}.ft202, ''), IFNULL(t3_{n}.ft302, '')) AS fx{n},
                            IF(d0.f00{n}1 = '', NULL, IF(d0.f003 = '2' AND d0.f00{n}3 <> 0, d0.f00{n}3, NULLIF(d0.f00{n}2, 0))) AS fh{n},
                            IF(d0.f003 = '2' AND d0.f00{n}1 <> '' AND d0.f00{n}3 <> 0, 1, 0) AS fr{n}";
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

                    SpravaDat.Select( "rozpis", cmd, new { datod = od, datdo = od.AddMonths( 1 ).AddDays( -1 ) } );
                }
                catch (Exception ex)
                {
                    JisLog.Chyba( ex );
                }
                finally { }

                return (proCmd.Stav == 0);
            }
        }

        private class MesicPolozka
        {
            public int Cislo { get; set; }
            public string Nazev { get; set; }
        }
    }
}
