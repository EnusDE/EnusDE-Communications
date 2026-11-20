# Host-Rollentest — Communications 0.6.8

Der Host kann die Kommunikation selbst aus TF-, FDL- und Admin-Sicht testen, obwohl sein echter Spieler als Inhaber immer Verwaltungsrechte hat.

## Starten und Rollen wechseln

1. Als Host eine Welt laden.
2. **Spieloptionen → Mods → EnusDE Communications → Host-Rollentest → Lokalen Rollentest öffnen** wählen.
3. Der Chat zeigt jetzt deutlich **LOKALER ROLLENTEST**. Die obere Testleiste bietet **Triebfahrzeugführer (TF)**, **Fahrdienstleiter (FDL)**, **Administrator** und **Rollentest beenden**.
4. Die TF-Testfigur ist bereits bei einer Testkopie der angemeldeten beziehungsweise ausgewählten Lok angemeldet. Ohne verfügbare Lok gibt es eine fiktive „Test 1“.
5. Beim Rollenwechsel bleiben die jeweiligen empfangenen Nachrichten und persönlichen Antwortmöglichkeiten erhalten. Das aktuelle Eingabefenster wird geschlossen; zum Lesen/Antworten die passende Unterhaltung wieder öffnen.

Die drei Testfiguren verwenden normale Standardrollen: TF ohne FDL-/Adminrechte, FDL mit Dispatcherrechten, Admin mit Verwaltungsrechten. Sie gehören derselben Testmannschaft an. Eigene Rangüberschreibungen des echten Servers werden dadurch nicht als identischer Testaufbau nachgebildet.

## Beide Richtungen testen

1. In **TF** unten **FDL** anklicken. Eine Frage schreiben, etwa „Darf ich von Bryson nach Sylva fahren?“, oder einen Baustein senden.
2. Oben auf **FDL** wechseln. Die eingegangene Anfrage öffnen oder die betreffende Lok im FDL-Fenster auswählen. Antworten, beispielsweise mit Warten, Freitext oder einem Baustein.
3. Zurück auf **TF** wechseln und die Antwort im Gespräch lesen. Danach erneut eine Frage stellen. Die Unterhaltung bleibt zugänglich und endet nicht mit der ersten Antwort.
4. Zum Quittieren auf **FDL** wechseln und nach der ersten Antwort eine zusätzliche Aufforderung wie „Bitte bestätigen“ senden. Dann auf **TF** wechseln, unten **FDL** öffnen und **Quittieren** anklicken. Die persönliche Antwortkarte verschwindet, der Gesprächsverlauf bleibt.
5. Für Global und Mannschaft den jeweiligen Reiter wählen und **+** öffnen. Global funktioniert ohne Lokanmeldung. Mannschaft bietet Aufgabenbausteine; bei einem Zugauftrag kann der Auftragszug unabhängig von der eigenen Anmeldung gewählt werden.
6. **Rollentest beenden** bringt den echten Chat zurück.

## Abgrenzung

Der Test benutzt Kopien der Lok-/Mannschaftsliste und drei eindeutig mit `[TEST]` bezeichnete Teilnehmer. Er verwendet die Chat-Routing- und Berechtigungslogik, sendet seine Nachrichten aber ausschließlich innerhalb dieser lokalen Testsitzung. Echte Spieler werden nicht übernommen oder umbenannt; Spielrollen, Lokzuordnungen und echte Chat-Archive werden durch die Testnachrichten nicht verändert. Die Testnachrichten werden nur im Arbeitsspeicher gehalten und beim Beenden verworfen.

Der echte Chat empfängt im Hintergrund weiter und erscheint nach dem Test wieder. Persönliche Anzeigeeinstellungen sind dieselben wie im echten Chat. Der Test ist eine Funktionsprüfung, keine zweite Steam-Instanz und keine Prüfung der MP-Hänger.

Eine laufende Leistungsmessung wird beim Start des Rollentests beendet und gespeichert. Für eine neue echte Leistungsmessung zuerst den Rollentest beenden. Bestehende Diagnose-CSVs bleiben erhalten.
