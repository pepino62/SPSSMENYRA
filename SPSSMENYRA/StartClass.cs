using JisResources;
using JisResources.Sources;
using SpsSpolecne;
using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace SPSSMENYRA
{
    class StartClass
    {

        [STAThread()]
        static void Main(string[] args)
        {

            JisApplication app = new JisApplication() { ShutdownMode = ShutdownMode.OnLastWindowClose };
            try
            {
                Goa.Start( "SpsLocal", 0 );
                args = Smena.Init( args );     // varianta RA / ATK (parametr prikazove radky), zbytek args pro ProjectBind

                // Nastavi podmmiky prihlaseni programu - bude pouzije args (externi volani) nebo pouzije soubor {pathExe}jisconfig.json
                Goa.ProjectBind = new ProjectBind( args );    // parametry : pole parametru (ext.volani), vychozi IdConnection, typ prihlaseni
                if (!string.IsNullOrEmpty( Goa.SetSource( Goa.StartIdConnection ) )) // nastavi provider, database, ....
                {
                    app.Run( new StartWindow() );
                }
                else
                {
                    if (!Goa.IsLoad)
                        app.Shutdown();
                    MessageBox.Show( $"Nepodařilo se spustit program\nMožný důvod:\nChyba datového připojení", "CHYBA SPUŠTĚNÍ PROGRAMU", MessageBoxButton.OK, MessageBoxImage.Error );
                }
            }
            catch (Exception ex)
            {
                if (!Goa.IsLoad)
                    app.Shutdown();
                MessageBox.Show( $"{ex.Message}\n\n{ex.Source}\n\n{ex.StackTrace}", "CHYBA PROGRAMU", MessageBoxButton.OK, MessageBoxImage.Error );
            }
            finally
            {
            }

        }

    }

    class StartWindow : JisWindow
    {

        public StartWindow()
        {
            try
            {
                jisBackground = new SolidColorBrush( (Color)ColorConverter.ConvertFromString( "#3B4F81" ) );  // fialova
                Name = Goa.AppName;                           // nutne napr. pro ulozeni velikosti apolohy
                Title = $"Sportis, {Smena.Titul} Služby ( ver: {Goa.Version} - {new FileInfo( Assembly.GetExecutingAssembly().Location ).LastWriteTime} )";
                SpravaForms.SetForm( "read", this, "" );    // nacte polohu a velikost formulare

                SetBlur( 100, br: new SolidColorBrush( (Color)ColorConverter.ConvertFromString( "#CAD3EF" ) ) );
                Closing += JisWindow_Closing;       // uzavre otevrena okna, ulozi polohu okna, ULOZI PARAMETRY
                Deactivated += JisWindow_Deactivated;       // ulozi pozici okna v okamziku deaktivace (pokud se zmenila) - je to pro okna volana jako externi proces - ta mohou byt ukoncena process.kill() - vynecha closing
                LocationChanged += JisWindow_LocationChanged;

            }
            catch (Exception ex)
            {
                Msg.Err( ex, id: "StartWindow" );
            }
        }


        /// <summary>
        /// musi byt pro spravne ukonceni programu (pokud mam Popup okno)
        /// dalsi kod jeste v App.xaml - ShutdownMode="OnExplicitShutdown"
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed( e );
            Application.Current.Shutdown();
        }
    }


    public class ProjectBind : ProjectBasic
    {
        public ProjectBind(string[] args) : base( args )
        {
        }



        public override void InitMethod()
        {
            // vstup jen pro role R.A. 002,100 / ATK 003,100 a spravce (VFP SPORTX - tlacitka R.A. SMĚNY / ATK SMĚNY)
            if (!Role.Ma( Smena.Role ))
            {
                Msg.Warning( $"Uživatel {Goa.UserName} nemá oprávnění pro práci s rozpisem služeb {Smena.Titul}.\n\nKontaktujte správce.", "NELZE SPUSTIT" );
                Environment.Exit( 0 );
            }

            AddPage( new Rozpis(), "  ROZPIS  " );
        }
    }

    #region <<< COLUMNS DYNAMIC SETTING - dynamicke vlastnosti sloupcu v DataGridu>>>
    public class DynamicColumnSet : DynamicColumn
    {
        // musi byt zavolano na Page - viz napr. Seznam()
        //   USERDATAGRID1.dynamicColumn = new DynamicColumnSet( );  // nastaveni dynamickych vlastnosti sloupcu (barva,font,..)
        //
        // CB - ColorBack
        // CF - ColorFore
        // FI - FontStyle
        // FW - FontWeight
        // FF - FontFamily
        // FS - FontSize

        public override object SettingColumn(string sekce, DataRow dataRow, string fieldName)
        {
            #region <<< SMENY_ROZPIS >>>
            switch (sekce)
            {
                // radek: casovy radek sedy, pracovni den zlutozeleny, sobota / nedele cervena (VFP SPORT0_GRD001)
                case "CB_SMENY_ROZPIS":
                    if ((string)dataRow[ "F003" ] == RozpisSluzba.CasovyRadek)
                        return "#EEEEEE";
                    switch (System.Convert.ToInt32( dataRow[ "FDAYN" ] ))
                    {
                        case 7: return "#FFC8C8";       // sobota
                        case 1: return "#FFA8A8";       // nedele
                        default: return "#F1F7C3";
                    }

                // hodiny - rucne opravene (f00n3) tyrkysove
                case "CB_SMENY_ROZPIS_HOD":
                    if (System.Convert.ToInt32( dataRow[ fieldName.ToUpper().Replace( "FH", "FR" ) ] ) == 1)
                        return "#00FFFF";
                    goto case "CB_SMENY_ROZPIS";

                // casovy radek modre kurzivou
                case "CF_SMENY_ROZPIS":
                    if ((string)dataRow[ "F003" ] == RozpisSluzba.CasovyRadek)
                        return "#1C3FB5";
                    break;

                case "FI_SMENY_ROZPIS":
                    if ((string)dataRow[ "F003" ] == RozpisSluzba.CasovyRadek)
                        return FontStyles.Italic;
                    break;

                case "FW_SMENY_ROZPIS":
                    if ((string)dataRow[ "F003" ] == RozpisSluzba.RadekDne)
                        return FontWeights.Bold;
                    break;

                // soucty pracovniku - radek Celkem zeleny (VFP SPORT0_GRD002)
                case "CB_SMENY_PRAC":
                    if ((string)dataRow[ "FTYP" ] == "2")
                        return "#00F080";
                    return "#CCE4FF";

                default:
                    return null;
            }
            #endregion
            return null;
        }

    }
    #endregion
}
