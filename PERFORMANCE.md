# MP-Hänger untersuchen — Communications 0.6.8 (Funktion seit 0.6.5)

Die Leistungsdiagnose zeichnet Bildabstände und Chat-Verarbeitungszeiten auf. Sie ist beim Host und beim Mitspieler verfügbar.

## Ein kurzer Test pro betroffenem PC

1. Mit geschlossenem Spiel 0.6.8 wie in der [Installationsanleitung](README.md#install) installieren. Einstellungen und Data erhalten. Host und Mitspieler verwenden für diesen Vergleich dieselbe Fassung.
2. Der MP-Sitzung beitreten beziehungsweise sie hosten. Unter **Spieloptionen → Mods → EnusDE Communications → Leistungsdiagnose** auf **60 Sekunden Leistungsmessung starten** klicken.
3. Menü schließen. Etwa 15 Sekunden wie bisher laufen, die Kamera drehen oder einen fahrenden Zug beobachten.
4. Dieselben Einstellungen erneut öffnen und **Chat 10 Sekunden pausieren** anklicken. Das Einstellungsfenster schließt; der lokale Chat verschwindet für zehn Sekunden und läuft anschließend automatisch weiter. In dieser Zeit dieselbe Bewegung wiederholen und darauf achten, ob die Stopps ausbleiben. Beim Host pausiert auch die Mod-Chat-Zustellung an Mitspieler; während des Vergleichs keine Nachrichten senden.
5. Noch etwa 15 Sekunden mit wieder aktivem Chat vergleichen. Die Messung endet nach insgesamt 60 Sekunden automatisch. Alternativ im Menü **Messung beenden und CSV speichern** anklicken.
6. Die neue CSV aus **EnusDE-Communications/Data/Diagnostics** zusammen mit der Angabe „Host/Client; Stopps mit Pause ja/nein“ weitergeben. Jede Person erzeugt ihre eigene Datei. Die Start-/Speichermeldung und der genaue Speicherpfad stehen zusätzlich im Player.log.

Danach kann ein unveränderter Vergleich ohne installierte Chat-Mod folgen, sofern die erste Messung die Ursache nicht eingrenzt. Dabei zunächst denselben Spielstand, dieselbe MP-Verbindung und dieselben übrigen Mods beibehalten. Erst danach einzelne weitere Mods getrennt vergleichen. Mehrere gleichzeitige Änderungen lassen keine klare Zuordnung zu.

## Was aufgezeichnet wird

Die Aufzeichnung startet ausschließlich per Knopfdruck und endet nach höchstens 60 Sekunden oder 12.000 Bildern. Sie ist standardmäßig aus und wird nicht als persönliche Einstellung gespeichert. Die Daten bleiben während der Messung in vorab reserviertem Arbeitsspeicher. Eine CSV wird erst am Ende, beim ausdrücklichen Beenden oder beim Verlassen der Welt geschrieben. Speicherreservierung und Export können jeweils eine einzelne Pause erzeugen; die Start-/Endgrenze ist deshalb bei der Auswertung auszunehmen.

Die CSV enthält keine Nachrichtentexte, Spielernamen, Steam-IDs oder Netzwerkadressen. Ältere Diagnose-CSVs werden nicht automatisch überschrieben oder gelöscht. Sie sind von den sechs Stunden aufbewahrten Host-Chatprotokollen getrennt.

| Spalte | Bedeutung |
| --- | --- |
| frame, elapsed_ms, utc_iso | Bildnummer und Zeitanker für den Vergleich mit Protokollen. |
| frame_gap_ms | Abstand zwischen diesem und dem vorherigen Beginn des Chat-Updates. Ein großer Wert liegt **nach** der Arbeit der vorherigen Zeile und kann Arbeit anderer Spielteile enthalten. Kein eigenständiger GPU-Profiler. |
| role, chat_connected, chat_suspended | Lokale Rolle, Chat-Handschlag und zehnsekündige Vergleichspause. |
| update_ms | Gesamte normale Chat-Update-Verarbeitung. |
| snapshot_ms, language_ms | Lesen der Spiel-/Rollen-/Zugliste und Erkennung der Sprache. |
| chat_poll_ms, chat_send_ms, chat_receive_ms | Dauer des Chat-Empfangs, des Sendens und der Paketverarbeitung. Empfangsverarbeitung ist in Poll enthalten. |
| ui_ms, ui_rebuild_ms | Chat-LateUpdate und Aufbau der Oberfläche. Der Aufbau ist gegebenenfalls bereits in ui_ms enthalten. |
| native_network_ms | Dauer von SteamClient.Update und SteamServer.Update des Spiels einschließlich Verarbeitung nativer Spielnachrichten, je nach lokaler Rolle. Eine lange Zeit beweist allein keinen Internetfehler. |
| archive_ms | Synchrones Schreiben oder Bereinigen des Host-Archivs. |
| input_captured, native_focus_pause | Vom Chat beanspruchte Eingabe beziehungsweise bereits vorhandene native Fokuspause. |
| sent_packets/bytes, received_packets/bytes | Nur Mengen im eigenen Chat-Kanal. |
| gc0, gc1, gc2 | Änderungen der prozessweiten GC-Zähler seit dem vorherigen Beginn. Sie weisen keine bestimmte Mod als Verursacher aus. |

Teilzeiten sind verschachtelt und dürfen nicht addiert werden. Während der zehnsekündigen Pause bleiben native Mehrspielerverbindung und Spiel aktiv; Chat-Polling, normale Chat-Updates, Oberfläche und Archivpflege ruhen lokal. Die Mod bleibt geladen, ebenso ihre Registrierungen. Der Vergleich ersetzt deshalb keinen Test mit vollständig entfernter Mod.

Ein wiederkehrender großer Bildabstand zusammen mit hoher Snapshot-/UI-/Archiv-/Pollzeit grenzt den betroffenen Bereich ein. Bleiben alle erfassten Chat-Zeiten kurz, ist zusätzlich die Arbeit außerhalb dieser Abschnitte beziehungsweise das Zusammenspiel der Mods zu untersuchen. Stabile Pingwerte oder ein geglätteter FPS-Zähler schließen kurze Hauptthread-Hänger nicht aus.

Die konkrete MP-Reproduktion und die tatsächliche Ursache sind noch offen. Die automatisierten Tests prüfen Messgrenzen, Felder, Bedienwege und unveränderte Paketübertragung; sie ersetzen keinen Zwei-PC-Test.

## Vor der Messung den lokalen Rollentest beenden

Der Host-Rollentest prüft Kommunikation lokal und bildet keine Steam-Verbindung nach. Deshalb kann währenddessen keine echte Leistungsmessung gestartet werden. Ein bereits laufender Mitschnitt wird beim Öffnen des Rollentests beendet und gespeichert. Für den Vergleich mit dem Mitspieler zuerst **Rollentest beenden**, dann wie oben messen.
