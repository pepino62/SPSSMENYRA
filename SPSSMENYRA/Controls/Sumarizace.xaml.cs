using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SPSSMENYRA
{
    /// <summary>
    /// Sumarizace roku - hodiny pracovniku po mesicich a za rok, dovolena planovana a cerpana (VFP SPORT0 / SPORTA - zalozka SUMARIZACE, SPORT0_3TMS)
    /// </summary>
    public partial class Sumarizace : JisUserControl
    {
        SumarizaceBind pageBase;

        public Sumarizace()
        {
            pageBase = new SumarizaceBind();
            InitializeComponent();
            DataContext = pageBase;
            pageBase.SetDataGrid( this, pageBase );
            pageBase.ReadControls( this );
            USERDATAGRID1.dynamicColumn = new DynamicColumnSet();   // radek Celkem, neaktivni pracovnici
        }

        public override void JisRefresh_Page()
        {
            if (Goa.IsInit( Name ) && pageBase.Status != EStatus.view)     // pri prvnim zobrazeni hned nacist
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
                using (new WaitCursor())
                {
                    if (pageBase.DataSource())       // naplni zdroj dat
                        USERDATAGRID1.RefreshSourceData( false );  // false - bez znovunaplneni dat
                }
            }
        }

        private void CboRok_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                Refresh();
        }

        private void BtnObnovit_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        // tisk prehledu sluzeb za rok (VFP RES01) - z nactenych dat zalozky
        private void BtnTisk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (pageBase.Status != EStatus.view || Goa.Table( "sumarizace" ) == null)
                    throw new JisException( "Nejdříve načtěte sumarizaci (OBNOVIT)" );
                using (new WaitCursor())
                    SmenyTisk.Prehled( pageBase.Rok, Goa.Table( "sumarizace" ) );
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


        private class SumarizaceBind : DataBindBasic
        {
            int _Rok = DateTime.Today.Year;
            public int Rok { get => _Rok; set => SetField( ref _Rok, value ); }

            /// <summary>
            /// roky 2016 .. pristi rok
            /// </summary>
            public List<int> Roky { get; } = new List<int>();

            public SumarizaceBind() : base()
            {
                AddMethod( () => { SaveParameters(); }, "CallCloseMethod" );

                for (int i = DateTime.Today.Year + 1; i >= 2016; i--)
                    Roky.Add( i );

                Init();
            }

            public void Init()
            {
                ParamClear();
                ParamAdd( () => Rok, DateTime.Today.Year );
                ReadParameters();
                if (!Roky.Contains( Rok ))
                    Rok = DateTime.Today.Year;
            }

            /// <summary>
            /// pracovnici s hodinami v roce (i neaktivni), hodiny po mesicich F01..F12, za rok FROK,
            /// <br>dovolena FDOVP planovana (d1.f102 = '1') a FDOVV cerpana (d1.f102 = '2') v roce + radek Celkem (VFP SPORT0_3TMS)</br>
            /// </summary>
            public bool DataSource()
            {
                try
                {
                    string mesice = string.Join( ",", Enumerable.Range( 1, 12 ).Select( m =>
                        $"CAST(SUM(IF(MONTH(x.f002) = {m}, x.hod, 0)) AS DECIMAL(7,1)) AS f{m:00}" ) );

                    string cmd = $@"
                        SELECT t3.ft301, t3.ft302, t3.ft304, t3.f0, '1' AS ftyp,
                            CAST(IFNULL(d.dovp, 0) AS DECIMAL(5,1)) AS fdovp,
                            CAST(IFNULL(d.dovv, 0) AS DECIMAL(5,1)) AS fdovv,
                            h.frok, {string.Join( ", ", Enumerable.Range( 1, 12 ).Select( m => $"h.f{m:00}" ) )}
                        FROM {Smena.T3} t3
                        JOIN (SELECT x.ft301, CAST(SUM(x.hod) AS DECIMAL(7,1)) AS frok, {mesice}
                              FROM ({RozpisSluzba.HodinySql( "@rokod", "@rokdo" )}) x GROUP BY x.ft301) h ON h.ft301 = t3.ft301 AND h.frok <> 0
                        LEFT JOIN (SELECT d1.ft301, SUM(IF(d1.f102 = '1', d1.f103, 0)) AS dovp, SUM(IF(d1.f102 = '2', d1.f103, 0)) AS dovv
                              FROM {Smena.D1} d1 WHERE YEAR(d1.f104) = @rok GROUP BY d1.ft301) d ON d.ft301 = t3.ft301
                        ORDER BY t3.ft302";

                    if (SpravaDat.Select( "sumarizace", cmd, new { rok = Rok, rokod = new DateTime( Rok, 1, 1 ), rokdo = new DateTime( Rok, 12, 31 ) } ))
                    {
                        DataTable dt = Goa.Table( "sumarizace" );
                        foreach (DataColumn c in dt.Columns)
                            c.ReadOnly = false;
                        DataRow celkem = dt.NewRow();
                        celkem[ "ft301" ] = "";
                        celkem[ "ft302" ] = "Celkem";
                        celkem[ "ft304" ] = 0;
                        celkem[ "f0" ] = "";
                        celkem[ "ftyp" ] = "2";
                        foreach (string f in new[] { "fdovp", "fdovv", "frok" }.Concat( Enumerable.Range( 1, 12 ).Select( m => $"f{m:00}" ) ))
                            celkem[ f ] = dt.AsEnumerable().Sum( r => Convert.ToDecimal( r[ f ] ) );
                        dt.Rows.Add( celkem );
                        dt.AcceptChanges();
                    }
                }
                catch (Exception ex)
                {
                    JisLog.Chyba( ex );
                }
                finally { }

                return (proCmd.Stav == 0);
            }
        }
    }
}
