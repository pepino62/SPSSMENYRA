using JisResources;
using JisResources.Sources;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SPSSMENYRA
{
    /// <summary>
    /// seznam pracovniku s dovolenou na rok - novy, oprava (karta), cerpani dovolene (VFP SPORT21 seznam osob, SPORT1, SPORT11)
    /// </summary>
    public partial class Pracovnici : JisUserControl
    {
        PracovniciBind pageBase;

        public Pracovnici()
        {
            pageBase = new PracovniciBind();
            InitializeComponent();
            DataContext = pageBase;
            pageBase.Stranka = this;
            pageBase.SetDataGrid( this, pageBase );
            pageBase.ReadControls( this );
            USERDATAGRID1.dynamicColumn = new DynamicColumnSet();   // vyrazeni pracovnici sede
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

        private void BtnObnovit_Click(object sender, RoutedEventArgs e) => Refresh();

        private void BtnNovy_Click(object sender, RoutedEventArgs e) => pageBase.Karta( "" );

        private void BtnOprava_Click(object sender, RoutedEventArgs e)
        {
            string ft301 = pageBase.Vybrany();
            if (ft301 == "")
                Msg.Ok( "Vyberte v seznamu pracovníka" );
            else
                pageBase.Karta( ft301 );
        }

        private void BtnDovolena_Click(object sender, RoutedEventArgs e) => pageBase.Dovolena();


        private class PracovniciBind : DataBindBasic
        {
            public Pracovnici Stranka { get; set; }

            int _Rok = DateTime.Today.Year;
            public int Rok { get => _Rok; set => SetField( ref _Rok, value ); }

            /// <summary>
            /// roky 2016 .. pristi rok
            /// </summary>
            public List<int> Roky { get; } = new List<int>();

            public PracovniciBind() : base()
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
            /// vsichni pracovnici (aktivni nahore), typ z t1, dovolena planovana / cerpana v roce (VFP SPORT21_1TMP)
            /// </summary>
            public bool DataSource()
            {
                try
                {
                    string cmd = $@"
                        SELECT t3.ft301, t3.f0, IF(t3.f0 = '1', 'X', '') AS f0x, t3.ft302, t3.ft303,
                            IFNULL(t1.fprompt, t3.ft307) AS ftyp, t3.ft304, t3.ft305, t3.ft306,
                            CAST(IFNULL(d.dovp, 0) AS DECIMAL(5,1)) AS fdovp,
                            CAST(IFNULL(d.dovv, 0) AS DECIMAL(5,1)) AS fdovv,
                            LEFT(t3.ft309, 100) AS fpozn
                        FROM {Smena.T3} t3
                        LEFT JOIN t1 ON t1.fsection = 'SPORT_OSTYP' AND t1.fitem = t3.ft307
                        LEFT JOIN (SELECT d1.ft301, SUM(IF(d1.f102 = '1', d1.f103, 0)) AS dovp, SUM(IF(d1.f102 = '2', d1.f103, 0)) AS dovv
                              FROM {Smena.D1} d1 WHERE YEAR(d1.f104) = @rok GROUP BY d1.ft301) d ON d.ft301 = t3.ft301
                        ORDER BY t3.f0, t3.ft302";
                    SpravaDat.Select( "pracseznam", cmd, new { rok = Rok } );
                }
                catch (Exception ex)
                {
                    JisLog.Chyba( ex );
                }
                finally { }

                return (proCmd.Stav == 0);
            }

            public string Vybrany()
            {
                DataRow row = Stranka.USERDATAGRID1.GetRow();
                return row == null ? "" : $"{row[ "ft301" ]}".Trim();
            }

            /// <summary>
            /// karta pracovnika - prazdny klic = novy (VFP SPORT1_CALL0)
            /// </summary>
            public void Karta(string ft301)
            {
                try
                {
                    if (new KartaPracovnika( ft301, Rok ).ShowDialog( Window.GetWindow( Stranka ) ))
                        Stranka.Refresh();
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

            /// <summary>
            /// cerpani dovolene vybraneho pracovnika (VFP SPORT11_CALL0)
            /// </summary>
            public void Dovolena()
            {
                DataRow row = Stranka.USERDATAGRID1.GetRow();
                if (row == null)
                {
                    Msg.Ok( "Vyberte v seznamu pracovníka" );
                    return;
                }
                new Dovolena( $"{row[ "ft301" ]}".Trim(), $"{row[ "ft302" ]}".Trim(), Rok ).ShowDialog( Window.GetWindow( Stranka ) );
                Stranka.Refresh();
            }

            /// <summary>
            /// dvojklik v seznamu - karta pracovnika
            /// </summary>
            public override int ExecuteCommand(object _object, ModifierKeys modifierKeys, DataBindBasic _baseCodeBind)
            {
                if (_object != Stranka?.USERDATAGRID1)
                    return base.ExecuteCommand( _object, modifierKeys, _baseCodeBind );
                string ft301 = Vybrany();
                if (ft301 != "")
                    Karta( ft301 );
                return 1;
            }
        }
    }
}
