using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;

namespace LNXcounter.Services;

/// <summary>The app speaks the language of Windows (French, Spanish, German; English for everything else). English texts are the keys:
/// anything without an entry stays English. Texts written in the XAML files are translated when they appear on screen.</summary>
public static class Loc
{
    /// <summary>Two letter language of Windows, or a test override: LNX_LANG=fr</summary>
    public static readonly string Lang = (Environment.GetEnvironmentVariable("LNX_LANG") is { Length: > 0 } o ? o : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName).ToLowerInvariant();
    static readonly int Col = Lang switch { "fr" => 0, "es" => 1, "de" => 2, _ => -1 };
    public static bool Translated => Col >= 0;

    public static string T(string en)
    {
        if (Col < 0 || string.IsNullOrEmpty(en)) return en;
        if (D.TryGetValue(en, out var v)) return v[Col];
        var t = en.Trim();                                   // keep the spaces around a text: " opened"
        if (t.Length != en.Length && D.TryGetValue(t, out v)) return en[..(en.Length - en.TrimStart().Length)] + v[Col] + en[(en.TrimEnd().Length)..];
        return en;
    }

    public static string F(string en, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(en), args);

    /// <summary>Numbers and dates in bindings follow the Windows regional format.</summary>
    public static void Install()
    {
        try { FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag))); } catch { }
    }

    // English -> (French, Spanish, German)
    static readonly Dictionary<string, string[]> D = new()
    {
        // ---- tabs and headings
        ["OVERVIEW"] = new[] { "APERÇU", "RESUMEN", "ÜBERSICHT" },
        ["DROPS"] = new[] { "DROPS", "DROPS", "DROPS" },
        ["SETTINGS"] = new[] { "PARAMÈTRES", "AJUSTES", "EINSTELLUNGEN" },
        ["Your Fortnite name"] = new[] { "Ton pseudo Fortnite", "Tu nombre de Fortnite", "Dein Fortnite-Name" },
        ["New update available"] = new[] { "Nouvelle mise à jour disponible", "Nueva actualización disponible", "Neues Update verfügbar" },
        ["CHESTS OPENED"] = new[] { "COFFRES OUVERTS", "COFRES ABIERTOS", "GEÖFFNETE TRUHEN" },
        ["BROKEN"] = new[] { "CASSÉS", "ROTOS", "ZERSTÖRT" },
        ["MATCHES"] = new[] { "PARTIES", "PARTIDAS", "MATCHES" },
        ["AVG / MATCH"] = new[] { "MOY. / PARTIE", "PROM. / PARTIDA", "Ø / MATCH" },
        ["RECENT MATCHES"] = new[] { "PARTIES RÉCENTES", "PARTIDAS RECIENTES", "LETZTE MATCHES" },
        ["opened"] = new[] { "ouverts", "abiertos", "geöffnet" },
        ["broken"] = new[] { "cassés", "rotos", "zerstört" },
        ["TREASURE HOURS"] = new[] { "HEURES DU TRÉSOR", "HORAS DEL TESORO", "SCHATZSTUNDEN" },
        ["Every Treasure Hour so far and announced, in Paris time"] = new[] { "Toutes les Heures du trésor passées et annoncées, à l'heure de Paris", "Todas las Horas del tesoro pasadas y anunciadas, en hora de París", "Alle bisherigen und angekündigten Schatzstunden, in Pariser Zeit" },
        ["Gold Outfit"] = new[] { "Tenue dorée", "Atuendo dorado", "Goldoutfit" },
        ["Vehicle & Decal"] = new[] { "Véhicule et décalque", "Vehículo y calcomanía", "Fahrzeug & Aufkleber" },
        ["All times are Paris time. Epic announces each window a few days ahead."] = new[] { "Toutes les heures sont à l'heure de Paris. Epic annonce chaque créneau quelques jours à l'avance.", "Todas las horas son hora de París. Epic anuncia cada franja con unos días de antelación.", "Alle Zeiten sind Pariser Zeit. Epic kündigt jedes Zeitfenster einige Tage vorher an." },
        ["OPENED BY TYPE"] = new[] { "OUVERTS PAR TYPE", "ABIERTOS POR TIPO", "GEÖFFNET NACH ART" },
        ["Normal chests"] = new[] { "Coffres normaux", "Cofres normales", "Normale Truhen" },
        ["Rare chests"] = new[] { "Coffres rares", "Cofres raros", "Seltene Truhen" },
        ["Sprite chests"] = new[] { "Coffres de sprites", "Cofres de sprites", "Sprite-Truhen" },
        ["Jewel sprite chests"] = new[] { "Coffres de sprites joyaux", "Cofres de sprites joya", "Juwelen-Sprite-Truhen" },
        ["Buried chests"] = new[] { "Coffres enterrés", "Cofres enterrados", "Vergrabene Truhen" },
        ["Wanted posters"] = new[] { "Avis de recherche", "Carteles de se busca", "Steckbriefe" },
        ["Relic chests"] = new[] { "Coffres de reliques", "Cofres de reliquias", "Relikt-Truhen" },
        ["Other chests"] = new[] { "Autres coffres", "Otros cofres", "Andere Truhen" },

        // ---- settings
        ["UPDATES"] = new[] { "MISES À JOUR", "ACTUALIZACIONES", "UPDATES" },
        ["Update now"] = new[] { "Mettre à jour", "Actualizar ahora", "Jetzt aktualisieren" },
        ["Check now"] = new[] { "Vérifier", "Buscar ahora", "Jetzt prüfen" },
        ["Check automatically"] = new[] { "Vérifier automatiquement", "Buscar automáticamente", "Automatisch prüfen" },
        ["Tip: running  LNXcounter.exe -up  in a terminal opened in the folder of the exe updates it too."] = new[] { "Astuce : lancer  LNXcounter.exe -up  dans un terminal ouvert dans le dossier de l'exe met aussi à jour l'appli.", "Truco: ejecutar  LNXcounter.exe -up  en una terminal abierta en la carpeta del exe también actualiza la app.", "Tipp: Wenn du  LNXcounter.exe -up  in einem Terminal im Ordner der exe startest, wird die App ebenfalls aktualisiert." },
        ["ADD CHESTS TO YOUR COUNT"] = new[] { "AJOUTER DES COFFRES À TON COMPTE", "AÑADIR COFRES A TU CUENTA", "TRUHEN ZUR ZÄHLUNG HINZUFÜGEN" },
        ["LNXcounter can only count the matches it finds as replay files on this PC. If you also play on another device, or played before you installed the app, add those chests here. They are counted as normal chests and are used for your drop odds too."] = new[] { "LNXcounter ne compte que les parties trouvées sous forme de replays sur ce PC. Si tu joues aussi sur un autre appareil, ou avant d'avoir installé l'appli, ajoute ces coffres ici. Ils comptent comme des coffres normaux et servent aussi aux probabilités de drop.", "LNXcounter solo cuenta las partidas que encuentra como repeticiones en este PC. Si también juegas en otro dispositivo, o jugaste antes de instalar la app, añade esos cofres aquí. Cuentan como cofres normales y también se usan para las probabilidades de drop.", "LNXcounter zählt nur Matches, die es als Replay-Dateien auf diesem PC findet. Wenn du auch auf einem anderen Gerät spielst oder vor der Installation gespielt hast, füge diese Truhen hier hinzu. Sie zählen als normale Truhen und fließen auch in deine Drop-Chancen ein." },
        ["Add"] = new[] { "Ajouter", "Añadir", "Hinzufügen" },
        ["Remove"] = new[] { "Retirer", "Quitar", "Entfernen" },
        ["OR SET YOUR EXACT TOTAL"] = new[] { "OU DÉFINIS TON TOTAL EXACT", "O FIJA TU TOTAL EXACTO", "ODER LEGE DEINE GENAUE GESAMTZAHL FEST" },
        ["Already know how many chests you opened this season? Type your total and LNXcounter adds whatever its replays did not count to reach it."] = new[] { "Tu sais déjà combien de coffres tu as ouverts cette saison ? Entre ton total et LNXcounter ajoute ce que ses replays n'ont pas compté pour l'atteindre.", "¿Ya sabes cuántos cofres abriste esta temporada? Escribe tu total y LNXcounter añade lo que sus repeticiones no contaron para llegar a él.", "Du weißt schon, wie viele Truhen du diese Saison geöffnet hast? Gib deine Gesamtzahl ein, und LNXcounter fügt hinzu, was seine Replays nicht gezählt haben." },
        ["Set total"] = new[] { "Définir le total", "Fijar total", "Gesamtzahl setzen" },
        ["START WITH WINDOWS"] = new[] { "DÉMARRER AVEC WINDOWS", "INICIAR CON WINDOWS", "MIT WINDOWS STARTEN" },
        ["Opens LNXcounter quietly in the tray when you log in to Windows, so every match is counted. You can also switch this from the right-click menu of the tray icon or of the overlay."] = new[] { "Ouvre LNXcounter discrètement dans la zone de notification quand tu te connectes à Windows, pour que chaque partie soit comptée. Tu peux aussi l'activer depuis le clic droit sur l'icône de la zone de notification ou sur l'overlay.", "Abre LNXcounter en silencio en la bandeja al iniciar sesión en Windows, para que se cuente cada partida. También puedes cambiarlo con el clic derecho en el icono de la bandeja o en el overlay.", "Öffnet LNXcounter beim Anmelden bei Windows leise im Infobereich, damit jedes Match gezählt wird. Du kannst das auch über das Rechtsklick-Menü des Tray-Symbols oder des Overlays umschalten." },
        ["Start LNXcounter with Windows"] = new[] { "Démarrer LNXcounter avec Windows", "Iniciar LNXcounter con Windows", "LNXcounter mit Windows starten" },
        ["TREASURE HOUR ALERT"] = new[] { "ALERTE HEURE DU TRÉSOR", "AVISO DE HORA DEL TESORO", "SCHATZSTUNDEN-ALARM" },
        ["A pop-up slides in at the top right of your screen even while you are in the game: when a Treasure Hour starts (with a short chime), and as a warning 30 minutes before (silent) and 10 minutes before (with the chime)."] = new[] { "Une notification glisse en haut à droite de l'écran, même en pleine partie : au début d'une Heure du trésor (avec un petit carillon), et en avertissement 30 minutes avant (silencieux) et 10 minutes avant (avec le carillon).", "Una notificación aparece arriba a la derecha de la pantalla incluso en pleno juego: cuando empieza una Hora del tesoro (con un breve sonido), y como aviso 30 minutos antes (en silencio) y 10 minutos antes (con el sonido).", "Ein Pop-up gleitet oben rechts auf deinen Bildschirm, auch mitten im Spiel: wenn eine Schatzstunde beginnt (mit kurzem Klang) und als Warnung 30 Minuten vorher (lautlos) und 10 Minuten vorher (mit Klang)." },
        ["Warn me 30 and 10 minutes before"] = new[] { "Me prévenir 30 et 10 minutes avant", "Avisarme 30 y 10 minutos antes", "30 und 10 Minuten vorher warnen" },
        ["Test"] = new[] { "Tester", "Probar", "Testen" },
        ["Alert me when a Treasure Hour starts"] = new[] { "Me prévenir quand une Heure du trésor commence", "Avisarme cuando empiece una Hora del tesoro", "Benachrichtigen, wenn eine Schatzstunde beginnt" },
        ["EXPORT"] = new[] { "EXPORT", "EXPORTAR", "EXPORT" },
        ["Save your chests for each day, week (Monday to Sunday) or month: matches, chests opened and broken, average and the treasures you found. CSV opens in Excel or Google Sheets and has the chests by type too; PDF is a small black and gold report. Chests added by hand get their own line."] = new[] { "Enregistre tes coffres par jour, semaine (du lundi au dimanche) ou mois : parties, coffres ouverts et cassés, moyenne et trésors trouvés. Le CSV s'ouvre dans Excel ou Google Sheets et contient aussi les coffres par type ; le PDF est un petit rapport noir et or. Les coffres ajoutés à la main ont leur propre ligne.", "Guarda tus cofres por día, semana (de lunes a domingo) o mes: partidas, cofres abiertos y rotos, promedio y tesoros encontrados. El CSV se abre en Excel o Google Sheets y incluye también los cofres por tipo; el PDF es un pequeño informe negro y dorado. Los cofres añadidos a mano tienen su propia línea.", "Speichere deine Truhen pro Tag, Woche (Montag bis Sonntag) oder Monat: Matches, geöffnete und zerstörte Truhen, Durchschnitt und gefundene Schätze. CSV öffnet sich in Excel oder Google Sheets und enthält auch die Truhen nach Art; PDF ist ein kleiner schwarz-goldener Bericht. Von Hand hinzugefügte Truhen bekommen eine eigene Zeile." },
        ["Per day"] = new[] { "Par jour", "Por día", "Pro Tag" },
        ["Per week"] = new[] { "Par semaine", "Por semana", "Pro Woche" },
        ["Per month"] = new[] { "Par mois", "Por mes", "Pro Monat" },
        ["BACKUP AND RESTORE"] = new[] { "SAUVEGARDE ET RESTAURATION", "COPIA DE SEGURIDAD Y RESTAURAR", "SICHERUNG UND WIEDERHERSTELLUNG" },
        ["Save all your stats (matches, chests added by hand, drops you ticked) to one file, and load it back on this or another PC, for example after reinstalling Windows. Restoring replaces the current stats and restarts the app."] = new[] { "Enregistre toutes tes stats (parties, coffres ajoutés à la main, drops cochés) dans un seul fichier, et recharge-le sur ce PC ou un autre, par exemple après une réinstallation de Windows. La restauration remplace les stats actuelles et redémarre l'appli.", "Guarda todas tus estadísticas (partidas, cofres añadidos a mano, drops marcados) en un solo archivo y cárgalo de nuevo en este u otro PC, por ejemplo tras reinstalar Windows. Restaurar reemplaza las estadísticas actuales y reinicia la app.", "Speichere alle deine Statistiken (Matches, von Hand hinzugefügte Truhen, abgehakte Drops) in einer Datei und lade sie auf diesem oder einem anderen PC wieder, zum Beispiel nach einer Neuinstallation von Windows. Das Wiederherstellen ersetzt die aktuellen Statistiken und startet die App neu." },
        ["Support on Ko-fi (optional)"] = new[] { "Soutenir sur Ko-fi (optionnel)", "Apoyar en Ko-fi (opcional)", "Auf Ko-fi unterstützen (optional)" },
        ["Chest map"] = new[] { "Carte des coffres", "Mapa de cofres", "Truhenkarte" },
        ["{0} · {1} on the map"] = new[] { "{0} · {1} sur la carte", "{0} · {1} en el mapa", "{0} · {1} auf der Karte" },
        ["{0} · {1} into the match"] = new[] { "{0} · {1} après le début", "{0} · {1} desde el inicio", "{0} · {1} nach Matchbeginn" },
        ["{0} (broken) · {1} into the match"] = new[] { "{0} (cassé) · {1} après le début", "{0} (roto) · {1} desde el inicio", "{0} (zerstört) · {1} nach Matchbeginn" },
        ["A chest is drawn where you stood when you opened it."] = new[] { "Un coffre est placé là où vous étiez quand vous l'avez ouvert.", "Un cofre se dibuja donde estabas al abrirlo.", "Eine Truhe wird dort gezeichnet, wo du beim Öffnen standest." },
        ["★ BEST MATCH · {0} chest · {1}"] = new[] { "★ MEILLEURE PARTIE · {0} coffre · {1}", "★ MEJOR PARTIDA · {0} cofre · {1}", "★ BESTES MATCH · {0} Truhe · {1}" },
        ["★ BEST MATCH · {0} chests · {1}"] = new[] { "★ MEILLEURE PARTIE · {0} coffres · {1}", "★ MEJOR PARTIDA · {0} cofres · {1}", "★ BESTES MATCH · {0} Truhen · {1}" },
        ["Best day {0}"] = new[] { "Meilleur jour {0}", "Mejor día {0}", "Bester Tag {0}" },
        ["Streak {0} day"] = new[] { "Série {0} jour", "Racha {0} día", "Serie {0} Tag" },
        ["Streak {0} days"] = new[] { "Série {0} jours", "Racha {0} días", "Serie {0} Tage" },
        [" (best {0})"] = new[] { " (record {0})", " (récord {0})", " (Rekord {0})" },
        ["GOAL REACHED · {0}/{1} today"] = new[] { "OBJECTIF ATTEINT · {0}/{1} aujourd'hui", "META CUMPLIDA · {0}/{1} hoy", "ZIEL ERREICHT · {0}/{1} heute" },
        ["GOAL REACHED · {0}/{1} this week"] = new[] { "OBJECTIF ATTEINT · {0}/{1} cette semaine", "META CUMPLIDA · {0}/{1} esta semana", "ZIEL ERREICHT · {0}/{1} diese Woche" },
        ["GOAL TODAY · {0}/{1}"] = new[] { "OBJECTIF DU JOUR · {0}/{1}", "META DE HOY · {0}/{1}", "TAGESZIEL · {0}/{1}" },
        ["GOAL THIS WEEK · {0}/{1}"] = new[] { "OBJECTIF DE LA SEMAINE · {0}/{1}", "META DE LA SEMANA · {0}/{1}", "WOCHENZIEL · {0}/{1}" },
        ["({0} during Treasure Hours)"] = new[] { "({0} pendant les Heures du Trésor)", "({0} durante las Horas del Tesoro)", "({0} während der Schatzstunden)" },
        ["Adding map data {0}/{1}…"] = new[] { "Ajout des données de carte {0}/{1}…", "Añadiendo datos del mapa {0}/{1}…", "Kartendaten werden hinzugefügt {0}/{1}…" },
        ["Show where the chests were"] = new[] { "Voir où étaient les coffres", "Ver dónde estaban los cofres", "Zeigen, wo die Truhen waren" },
        ["GOAL (OPTIONAL)"] = new[] { "OBJECTIF (OPTIONNEL)", "META (OPCIONAL)", "ZIEL (OPTIONAL)" },
        ["Set yourself a target and a progress bar appears above your matches on the Overview. Only chests counted from replays are used."] = new[] { "Fixez-vous un objectif et une barre de progression apparaît au-dessus de vos parties dans l'aperçu. Seuls les coffres comptés depuis les replays sont utilisés.", "Ponte una meta y aparecerá una barra de progreso sobre tus partidas en el resumen. Solo cuentan los cofres leídos de las repeticiones.", "Setze dir ein Ziel und über deinen Matches erscheint ein Fortschrittsbalken. Nur aus Replays gezählte Truhen zählen." },
        ["No goal"] = new[] { "Aucun objectif", "Sin meta", "Kein Ziel" },
        ["Set target"] = new[] { "Définir", "Fijar meta", "Ziel setzen" },
        ["Back up"] = new[] { "Sauvegarder", "Guardar copia", "Sichern" },
        ["Restore"] = new[] { "Restaurer", "Restaurar", "Wiederherstellen" },
        ["Chests added by hand are kept when you press Rescan all, and cleared by Reset."] = new[] { "Les coffres ajoutés à la main sont conservés quand tu cliques sur Tout relire, et effacés par Réinitialiser.", "Los cofres añadidos a mano se conservan al pulsar Releer todo y se borran con Restablecer.", "Von Hand hinzugefügte Truhen bleiben bei „Alles neu einlesen“ erhalten und werden durch „Zurücksetzen“ gelöscht." },

        // ---- drops page
        ["Mark as found"] = new[] { "Marquer comme trouvé", "Marcar como encontrado", "Als gefunden markieren" },
        ["DROP ODDS"] = new[] { "PROBABILITÉS DE DROP", "PROBABILIDADES DE DROP", "DROP-CHANCEN" },
        ["Each chest you open in a normal match"] = new[] { "Chaque coffre que tu ouvres dans une partie normale", "Cada cofre que abres en una partida normal", "Jede Truhe, die du in einem normalen Match öffnest" },
        ["EXPECTED FROM YOUR CHESTS"] = new[] { "ATTENDU AVEC TES COFFRES", "ESPERADO CON TUS COFRES", "ERWARTET MIT DEINEN TRUHEN" },
        ["Hover a tile to see what it is, tick the box when you find a drop."] = new[] { "Survole une case pour voir ce que c'est, coche-la quand tu trouves un drop.", "Pasa el ratón sobre una casilla para ver qué es y márcala cuando encuentres un drop.", "Fahre über ein Feld, um zu sehen, was es ist, und hake es ab, wenn du einen Drop findest." },
        ["Overlay"] = new[] { "Overlay", "Overlay", "Overlay" },
        ["Folder…"] = new[] { "Dossier…", "Carpeta…", "Ordner…" },
        ["Re-read recent"] = new[] { "Relire les récents", "Releer recientes", "Neuere neu lesen" },
        ["Re-reads the replays still on your PC and keeps the older matches as they are."] = new[] { "Relit les replays encore sur ton PC et garde les anciens matchs tels quels.", "Vuelve a leer los replays que siguen en tu PC y conserva las partidas antiguas.", "Liest die noch vorhandenen Replays neu und behält ältere Matches." },
        ["Re-read the replays still on your PC with the latest detection? Matches whose replay is gone are kept as they are."] = new[] { "Relire les replays encore sur ton PC avec la dernière détection ? Les matchs dont le replay a disparu sont conservés.", "¿Releer los replays que siguen en tu PC con la última detección? Las partidas sin replay se conservan.", "Vorhandene Replays mit der neuesten Erkennung neu lesen? Matches ohne Replay bleiben erhalten." },
        ["Re-reading match {0}/{1}…"] = new[] { "Relecture du match {0}/{1}…", "Releyendo partida {0}/{1}…", "Lese Match {0}/{1} neu…" },
        ["Re-read {0} matches, {1} changed"] = new[] { "{0} matchs relus, {1} modifiés", "{0} partidas releídas, {1} cambiadas", "{0} Matches neu gelesen, {1} geändert" },
        ["Rescan all"] = new[] { "Tout relire", "Releer todo", "Alles neu einlesen" },
        ["Reset"] = new[] { "Réinitialiser", "Restablecer", "Zurücksetzen" },
        ["▶  PREVIEW"] = new[] { "▶  APERÇU", "▶  VISTA PREVIA", "▶  VORSCHAU" },
        ["Volume"] = new[] { "Volume", "Volumen", "Lautstärke" },
        ["Open on YouTube ↗"] = new[] { "Ouvrir sur YouTube ↗", "Abrir en YouTube ↗", "Auf YouTube öffnen ↗" },
        ["The token is the same in every week, so one tick counts everywhere."] = new[] { "Le jeton est le même chaque semaine, donc une coche vaut partout.", "La ficha es la misma cada semana, así que una marca vale en todas.", "Das Token ist in jeder Woche dasselbe, ein Haken gilt also überall." },
        ["Treasure Hours"] = new[] { "Heures du trésor", "Horas del tesoro", "Schatzstunden" },
        ["Treasure Hour"] = new[] { "Heure du trésor", "Hora del tesoro", "Schatzstunde" },
        ["TREASURE HOUR"] = new[] { "HEURE DU TRÉSOR", "HORA DEL TESORO", "SCHATZSTUNDE" },
        ["LIVE NOW"] = new[] { "EN DIRECT", "EN DIRECTO", "JETZT LIVE" },

        // ---- Treasure Hour rows
        ["STARTS IN"] = new[] { "COMMENCE DANS", "EMPIEZA EN", "BEGINNT IN" },
        ["ENDS IN"] = new[] { "FINIT DANS", "TERMINA EN", "ENDET IN" },
        ["Ends in "] = new[] { "Fin dans ", "Termina en ", "Endet in " },
        ["In "] = new[] { "Dans ", "En ", "In " },
        ["ENDED"] = new[] { "TERMINÉE", "TERMINADA", "BEENDET" },
        ["NEXT TREASURE HOUR"] = new[] { "PROCHAINE HEURE DU TRÉSOR", "PRÓXIMA HORA DEL TESORO", "NÄCHSTE SCHATZSTUNDE" },
        ["UPCOMING"] = new[] { "À VENIR", "PRÓXIMA", "BALD" },
        ["NO UPCOMING HOUR"] = new[] { "AUCUNE HEURE À VENIR", "NINGUNA HORA PRÓXIMA", "KEINE STUNDE GEPLANT" },
        ["Nothing announced yet"] = new[] { "Rien d'annoncé pour l'instant", "Nada anunciado todavía", "Noch nichts angekündigt" },
        ["none scheduled"] = new[] { "aucune prévue", "ninguna prevista", "keine geplant" },
        ["CONFIRMED"] = new[] { "CONFIRMÉE", "CONFIRMADA", "BESTÄTIGT" },
        ["ESTIMATED"] = new[] { "ESTIMÉE", "ESTIMADA", "GESCHÄTZT" },
        ["ends in {0}"] = new[] { "finit dans {0}", "termina en {0}", "endet in {0}" },
        ["starts in {0}"] = new[] { "commence dans {0}", "empieza en {0}", "beginnt in {0}" },
        ["LIVE · ends {0}"] = new[] { "EN DIRECT · fin {0}", "EN DIRECTO · termina {0}", "LIVE · endet {0}" },

        // ---- status and messages
        ["Starting…"] = new[] { "Démarrage…", "Iniciando…", "Starte…" },
        ["counted from your replays + {0} added by hand"] = new[] { "compté depuis tes replays + {0} ajoutés à la main", "contados de tus repeticiones + {0} añadidos a mano", "aus deinen Replays gezählt + {0} von Hand hinzugefügt" },
        ["counted from your replays, only the ones you opened yourself"] = new[] { "compté depuis tes replays, seulement ceux que tu as ouverts toi-même", "contados de tus repeticiones, solo los que abriste tú", "aus deinen Replays gezählt, nur die, die du selbst geöffnet hast" },
        ["Added by hand so far: {0}   ·   Counted from replays: {1}   ·   Total: {2}"] = new[] { "Ajoutés à la main : {0}   ·   Comptés depuis les replays : {1}   ·   Total : {2}", "Añadidos a mano: {0}   ·   Contados de repeticiones: {1}   ·   Total: {2}", "Von Hand hinzugefügt: {0}   ·   Aus Replays gezählt: {1}   ·   Gesamt: {2}" },
        ["Nothing added by hand yet   ·   Counted from replays: {0}"] = new[] { "Rien ajouté à la main   ·   Comptés depuis les replays : {0}", "Nada añadido a mano   ·   Contados de repeticiones: {0}", "Noch nichts von Hand hinzugefügt   ·   Aus Replays gezählt: {0}" },
        ["That file is not a LNXcounter backup."] = new[] { "Ce fichier n'est pas une sauvegarde LNXcounter.", "Ese archivo no es una copia de LNXcounter.", "Diese Datei ist keine LNXcounter-Sicherung." },
        ["That file is not a LNXcounter backup (or it is empty)."] = new[] { "Ce fichier n'est pas une sauvegarde LNXcounter (ou il est vide).", "Ese archivo no es una copia de LNXcounter (o está vacío).", "Diese Datei ist keine LNXcounter-Sicherung (oder sie ist leer)." },
        ["Checking..."] = new[] { "Vérification...", "Comprobando...", "Prüfe..." },
        ["New update available {0}"] = new[] { "Nouvelle mise à jour disponible {0}", "Nueva actualización disponible {0}", "Neues Update verfügbar {0}" },
        ["You have v{0}. Press Update to download it ({1} MB) and restart."] = new[] { "Tu as la v{0}. Clique sur Mettre à jour pour la télécharger ({1} Mo) et redémarrer.", "Tienes la v{0}. Pulsa Actualizar para descargarla ({1} MB) y reiniciar.", "Du hast v{0}. Klicke auf Aktualisieren, um sie herunterzuladen ({1} MB) und neu zu starten." },
        ["You have the newest version."] = new[] { "Tu as la dernière version.", "Tienes la versión más reciente.", "Du hast die neueste Version." },
        ["Downloading {0}... {1}%"] = new[] { "Téléchargement de {0}... {1} %", "Descargando {0}... {1} %", "Lade {0} herunter... {1} %" },
        ["Updated. Restarting..."] = new[] { "Mis à jour. Redémarrage...", "Actualizado. Reiniciando...", "Aktualisiert. Starte neu..." },
        ["Backup saved: {0}."] = new[] { "Sauvegarde enregistrée : {0}.", "Copia guardada: {0}.", "Sicherung gespeichert: {0}." },
        ["Could not save the backup: "] = new[] { "Impossible d'enregistrer la sauvegarde : ", "No se pudo guardar la copia: ", "Sicherung konnte nicht gespeichert werden: " },
        ["Saved {0}."] = new[] { "{0} enregistré.", "{0} guardado.", "{0} gespeichert." },
        ["Could not save the file: "] = new[] { "Impossible d'enregistrer le fichier : ", "No se pudo guardar el archivo: ", "Datei konnte nicht gespeichert werden: " },
        ["There is nothing added by hand to remove."] = new[] { "Il n'y a rien d'ajouté à la main à retirer.", "No hay nada añadido a mano que quitar.", "Es gibt nichts von Hand Hinzugefügtes zu entfernen." },
        ["Nothing changed."] = new[] { "Rien n'a changé.", "No cambió nada.", "Nichts geändert." },
        ["Added {0} chests. Your total is now {1}."] = new[] { "{0} coffres ajoutés. Ton total est maintenant de {1}.", "Se añadieron {0} cofres. Tu total ahora es {1}.", "{0} Truhen hinzugefügt. Deine Gesamtzahl ist jetzt {1}." },
        ["Removed {0} chests. Your total is now {1}."] = new[] { "{0} coffres retirés. Ton total est maintenant de {1}.", "Se quitaron {0} cofres. Tu total ahora es {1}.", "{0} Truhen entfernt. Deine Gesamtzahl ist jetzt {1}." },
        ["LNXcounter already counted {0} chests from your replays, so your total can't be lower than that."] = new[] { "LNXcounter a déjà compté {0} coffres depuis tes replays, ton total ne peut donc pas être plus bas.", "LNXcounter ya contó {0} cofres de tus repeticiones, así que tu total no puede ser menor.", "LNXcounter hat bereits {0} Truhen aus deinen Replays gezählt, deine Gesamtzahl kann also nicht niedriger sein." },
        ["Done. Your total is now {0} ({1} from replays + {2} added by hand)."] = new[] { "C'est fait. Ton total est maintenant de {0} ({1} depuis les replays + {2} ajoutés à la main).", "Hecho. Tu total ahora es {0} ({1} de repeticiones + {2} añadidos a mano).", "Fertig. Deine Gesamtzahl ist jetzt {0} ({1} aus Replays + {2} von Hand hinzugefügt)." },
        ["Type a whole number above 0."] = new[] { "Entre un nombre entier supérieur à 0.", "Escribe un número entero mayor que 0.", "Gib eine ganze Zahl größer als 0 ein." },
        ["This session: {0}  ·  Last match: +{1}"] = new[] { "Cette session : {0}  ·  Dernière partie : +{1}", "Esta sesión: {0}  ·  Última partida: +{1}", "Diese Sitzung: {0}  ·  Letztes Match: +{1}" },
        ["This session: {0}"] = new[] { "Cette session : {0}", "Esta sesión: {0}", "Diese Sitzung: {0}" },
        ["Replay folder not found. Click Folder… to choose it."] = new[] { "Dossier des replays introuvable. Clique sur Dossier… pour le choisir.", "No se encuentra la carpeta de repeticiones. Pulsa Carpeta… para elegirla.", "Replay-Ordner nicht gefunden. Klicke auf Ordner…, um ihn auszuwählen." },
        ["Reading match {0}/{1}…"] = new[] { "Lecture de la partie {0}/{1}…", "Leyendo partida {0}/{1}…", "Lese Match {0}/{1}…" },
        ["Watching for new matches  ·  {0} replay(s) could not be read (game update?)"] = new[] { "Surveillance des nouvelles parties  ·  {0} replay(s) illisible(s) (mise à jour du jeu ?)", "Buscando nuevas partidas  ·  no se pudieron leer {0} repetición(es) (¿actualización del juego?)", "Warte auf neue Matches  ·  {0} Replay(s) nicht lesbar (Spiel-Update?)" },
        ["Match in progress, it will be counted as soon as it ends"] = new[] { "Partie en cours, elle sera comptée dès qu'elle se termine", "Partida en curso, se contará en cuanto termine", "Match läuft, es wird gezählt, sobald es endet" },
        ["Watching for new matches"] = new[] { "Surveillance des nouvelles parties", "Buscando nuevas partidas", "Warte auf neue Matches" },
        ["Scan error: "] = new[] { "Erreur d'analyse : ", "Error de escaneo: ", "Scan-Fehler: " },
        ["  ★ {0} treasure"] = new[] { "  ★ {0} trésor", "  ★ {0} tesoro", "  ★ {0} Schatz" },
        ["  ★ {0} treasures"] = new[] { "  ★ {0} trésors", "  ★ {0} tesoros", "  ★ {0} Schätze" },
        ["★ You"] = new[] { "★ Toi", "★ Tú", "★ Du" },
        ["☆ Nearby player"] = new[] { "☆ Joueur proche", "☆ Jugador cercano", "☆ Spieler in der Nähe" },
        ["{0}: {1} ({2} into the match)"] = new[] { "{0} : {1} ({2} après le début de la partie)", "{0}: {1} ({2} desde el inicio de la partida)", "{0}: {1} ({2} nach Matchbeginn)" },

        // ---- drops VM
        ["Not announced yet"] = new[] { "Pas encore annoncé", "Aún no anunciado", "Noch nicht angekündigt" },
        ["UNKNOWN"] = new[] { "INCONNU", "DESCONOCIDO", "UNBEKANNT" },
        ["SUPREME PRIZE"] = new[] { "PRIX SUPRÊME", "PREMIO SUPREMO", "HAUPTPREIS" },
        ["MAJOR PRIZE"] = new[] { "GROS LOT", "PREMIO MAYOR", "GROSSER PREIS" },
        ["STANDARD PRIZE"] = new[] { "LOT STANDARD", "PREMIO ESTÁNDAR", "STANDARDPREIS" },
        ["TREASURE HOUR  {0}×"] = new[] { "HEURE DU TRÉSOR  {0}×", "HORA DEL TESORO  {0}×", "SCHATZSTUNDE  {0}×" },
        ["WEEK {0}"] = new[] { "SEMAINE {0}", "SEMANA {0}", "WOCHE {0}" },
        ["WEEK {0} · {1}"] = new[] { "SEMAINE {0} · {1}", "SEMANA {0} · {1}", "WOCHE {0} · {1}" },
        ["{0}/{1} FOUND"] = new[] { "{0}/{1} TROUVÉS", "{0}/{1} ENCONTRADOS", "{0}/{1} GEFUNDEN" },
        ["1 in {0} per chest ({1})  ·  Treasure Hour {2}× (normally 1 in {3})"] = new[] { "1 sur {0} par coffre ({1})  ·  Heure du trésor {2}× (normalement 1 sur {3})", "1 de cada {0} por cofre ({1})  ·  Hora del tesoro {2}× (normalmente 1 de cada {3})", "1 von {0} pro Truhe ({1})  ·  Schatzstunde {2}× (normal 1 von {3})" },
        ["1 in {0} per chest ({1})"] = new[] { "1 sur {0} par coffre ({1})", "1 de cada {0} por cofre ({1})", "1 von {0} pro Truhe ({1})" },
        ["Treasure gizmo"] = new[] { "Gadget de trésor", "Gadget del tesoro", "Schatz-Gizmo" },
        [" for any gizmo"] = new[] { " pour n'importe quel gadget", " para cualquier gadget", " für irgendein Gizmo" },
        ["Song: "] = new[] { "Chanson : ", "Canción: ", "Song: " },
        ["With your {0} chests there is a {1}% chance you would have found one by now."] = new[] { "Avec tes {0} coffres, il y a {1} % de chances que tu en aies déjà trouvé un.", "Con tus {0} cofres hay un {1} % de probabilidad de que ya hubieras encontrado uno.", "Mit deinen {0} Truhen besteht eine Chance von {1} %, dass du schon einen gefunden hättest." },
        ["1 in {0}"] = new[] { "1 sur {0}", "1 de cada {0}", "1 von {0}" },
        ["Open some chests and your expected drops will show up here."] = new[] { "Ouvre quelques coffres et tes drops attendus apparaîtront ici.", "Abre algunos cofres y tus drops esperados aparecerán aquí.", "Öffne ein paar Truhen, dann erscheinen hier deine erwarteten Drops." },
        ["At {0} chests you'd expect about:  "] = new[] { "À {0} coffres, tu devrais avoir environ :  ", "Con {0} cofres esperarías unos:  ", "Bei {0} Truhen würdest du etwa erwarten:  " },

        // ---- tray / overlay menu
        ["Open LNXcounter"] = new[] { "Ouvrir LNXcounter", "Abrir LNXcounter", "LNXcounter öffnen" },
        ["Treasure Hours: {0}"] = new[] { "Heures du trésor : {0}", "Horas del tesoro: {0}", "Schatzstunden: {0}" },
        ["Total: {0} chests opened"] = new[] { "Total : {0} coffres ouverts", "Total: {0} cofres abiertos", "Gesamt: {0} Truhen geöffnet" },
        ["Show overlay"] = new[] { "Afficher l'overlay", "Mostrar overlay", "Overlay anzeigen" },
        ["Collapse details"] = new[] { "Réduire les détails", "Contraer detalles", "Details einklappen" },
        ["Move to corner"] = new[] { "Déplacer dans un coin", "Mover a una esquina", "In eine Ecke verschieben" },
        ["Top left"] = new[] { "En haut à gauche", "Arriba a la izquierda", "Oben links" },
        ["Top right"] = new[] { "En haut à droite", "Arriba a la derecha", "Oben rechts" },
        ["Bottom left"] = new[] { "En bas à gauche", "Abajo a la izquierda", "Unten links" },
        ["Bottom right"] = new[] { "En bas à droite", "Abajo a la derecha", "Unten rechts" },
        ["Start with Windows"] = new[] { "Démarrer avec Windows", "Iniciar con Windows", "Mit Windows starten" },
        ["Scan for new matches now"] = new[] { "Chercher de nouvelles parties maintenant", "Buscar nuevas partidas ahora", "Jetzt nach neuen Matches suchen" },
        ["Rebuild from all replays…"] = new[] { "Tout reconstruire depuis les replays…", "Reconstruir desde todas las repeticiones…", "Aus allen Replays neu aufbauen…" },
        ["Open replays folder"] = new[] { "Ouvrir le dossier des replays", "Abrir carpeta de repeticiones", "Replay-Ordner öffnen" },
        ["Open save folder"] = new[] { "Ouvrir le dossier de sauvegarde", "Abrir carpeta de guardado", "Speicherordner öffnen" },
        ["Quit"] = new[] { "Quitter", "Salir", "Beenden" },

        // ---- dialogs
        ["Re-read every replay in the folder and rebuild your totals from scratch?"] = new[] { "Relire tous les replays du dossier et reconstruire tes totaux de zéro ?", "¿Releer todas las repeticiones de la carpeta y reconstruir tus totales desde cero?", "Alle Replays im Ordner neu einlesen und deine Summen von Grund auf neu berechnen?" },
        ["Set all counters to zero, including chests you added by hand? Existing replays will not be counted again, only new matches."] = new[] { "Remettre tous les compteurs à zéro, y compris les coffres ajoutés à la main ? Les replays existants ne seront pas recomptés, seulement les nouvelles parties.", "¿Poner todos los contadores a cero, incluidos los cofres añadidos a mano? Las repeticiones existentes no se volverán a contar, solo las partidas nuevas.", "Alle Zähler auf null setzen, auch die von Hand hinzugefügten Truhen? Vorhandene Replays werden nicht erneut gezählt, nur neue Matches." },
        ["Replace your current stats with this backup? The current stats are kept as state.json.bak. The app restarts afterwards."] = new[] { "Remplacer tes stats actuelles par cette sauvegarde ? Les stats actuelles sont conservées dans state.json.bak. L'appli redémarre ensuite.", "¿Reemplazar tus estadísticas actuales con esta copia? Las actuales se guardan como state.json.bak. Después la app se reinicia.", "Deine aktuellen Statistiken durch diese Sicherung ersetzen? Die aktuellen bleiben als state.json.bak erhalten. Danach startet die App neu." },
        ["Save your chests as CSV"] = new[] { "Enregistrer tes coffres en CSV", "Guardar tus cofres como CSV", "Truhen als CSV speichern" },
        ["Save your chests as PDF"] = new[] { "Enregistrer tes coffres en PDF", "Guardar tus cofres como PDF", "Truhen als PDF speichern" },
        ["Save a backup of your stats"] = new[] { "Enregistrer une sauvegarde de tes stats", "Guardar una copia de tus estadísticas", "Sicherung deiner Statistiken speichern" },
        ["Choose a LNXcounter backup"] = new[] { "Choisir une sauvegarde LNXcounter", "Elegir una copia de LNXcounter", "LNXcounter-Sicherung auswählen" },
        ["Choose the Fortnite replays folder (Demos)"] = new[] { "Choisir le dossier des replays Fortnite (Demos)", "Elegir la carpeta de repeticiones de Fortnite (Demos)", "Fortnite-Replay-Ordner (Demos) auswählen" },

        // ---- toast
        ["STARTS IN {0} MINUTES"] = new[] { "COMMENCE DANS {0} MINUTES", "EMPIEZA EN {0} MINUTOS", "BEGINNT IN {0} MINUTEN" },
        ["Starts at {0} Paris time  ·  click to close"] = new[] { "Commence à {0} (heure de Paris)  ·  clique pour fermer", "Empieza a las {0} (hora de París)  ·  haz clic para cerrar", "Beginnt um {0} Pariser Zeit  ·  zum Schließen klicken" },
        ["Until {0} Paris time  ·  click to close"] = new[] { "Jusqu'à {0} (heure de Paris)  ·  clique pour fermer", "Hasta las {0} (hora de París)  ·  haz clic para cerrar", "Bis {0} Pariser Zeit  ·  zum Schließen klicken" },
        ["Outfit"] = new[] { "Tenue", "Atuendo", "Outfit" },
        ["Pirate Shop"] = new[] { "Boutique pirate", "Tienda pirata", "Piratenshop" },
        ["Vehicle"] = new[] { "Véhicule", "Vehículo", "Fahrzeug" },
    };
}

/// <summary>{loc:Tr 'Some text'} in XAML: the text in the language of Windows.</summary>
[MarkupExtensionReturnType(typeof(string))]
public class TrExtension : MarkupExtension
{
    public string Text { get; set; } = "";
    public TrExtension() { }
    public TrExtension(string text) { Text = text; }
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Text);
}
