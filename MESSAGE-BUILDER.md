# Kommunikation und Bausteine — Testversion 0.6.8

Für Railroader 0.6.3. Host und Chatnutzer verwenden gemeinsam **Communications 0.6.8 / Chat-Protokoll 4**. Ältere Chat-Versionen verbinden ihren Chat nicht mit dieser Fassung. Die native Spielteilnahme ohne Mod bleibt erlaubt. Data und ModSettings beim Update erhalten.

## Die vier Kommunikationswege

| Bereich | Zweck und Bausteine | Voraussetzung |
| --- | --- | --- |
| Global | Begrüßung, Hilfe erbitten/anbieten, Hilfe an einem Ort, Bedanken; außerdem freier Chat | Keine Lokanmeldung |
| Mannschaft | Hilfe und Arbeitsverteilung: Aufgabe übernehmen oder einem Zug Arbeit geben | Mitgliedschaft beziehungsweise erlaubte Mannschaftsansicht; allgemeine Meldungen ohne Lokanmeldung |
| Zug | Fahrt, Bereitschaft, Ankunft und Rangieren; F10 für kurze Rangiermeldungen | Beim Bezugstriebfahrzeug angemeldet |
| FDL | Dauerhaftes Gespräch in beide Richtungen, Fahrtanfragen, Warten/Freigeben und Quittierungen | TF beim Zug angemeldet; FDL darf Züge gezielt ansprechen |

**F10 bleibt ausschließlich im Zugkanal.** Die neuen Global-Bausteine werden über das Nachrichtenfenster geöffnet.

## Eine Bausteinnachricht senden

1. Den passenden Chat-Reiter wählen und **+ über dem Chat** anklicken. Alternativ im Fenster Global, Mannschaft, Zug oder FDL auswählen.
2. Meldungsart wählen. Nur tatsächlich benötigte Felder sind aktiv. Bei Zug-/FDL-Meldungen wird die Lokbezeichnung aus dem angemeldeten beziehungsweise angesprochenen Zug ergänzt.
3. Orte über **…** aus den Fahrplanstationen wählen oder eigene Orts-/Gleisnamen eingeben. Eigennamen bleiben unübersetzt.
4. Bei Mannschaftsaufgaben über die Aufgabenschaltfläche Wagen abholen, Wagen zustellen, Zug zusammenstellen, Rangieren oder Eigene Aufgabe wählen. Feste Aufgaben werden beim Empfänger übersetzt; eigener Aufgabentext bleibt wörtlich erhalten.
5. Bei **Zug einen Auftrag geben** mit **Auftragszug wählen** eine verfügbare Lok der Mannschaft wählen. Dadurch wird die eigene Zuganmeldung nicht verändert. Die Nachricht geht an die Mannschaft, die über den Auftrag informiert werden soll.
6. Vorschau prüfen und **Senden** anklicken. Bei bestätigter Zustellung schließt das Fenster. Bei einem Fehler bleiben die Orts-/Aufgabenwerte erhalten. Unbestätigte Nachrichten werden nicht automatisch erneut gesendet.

Die Meldung geht in den ausgewählten Kanal, auch wenn gerade ein anderer Reiter im Hauptchat angezeigt wird. **Aa**, Chat-Taste und Sprechblasensymbol dienen weiterhin freier Texteingabe. Das Plus kann weiterhin als Griff zum Verschieben des Hauptchats gezogen werden. Dialoge lassen sich an der Titelzeile bewegen.

Nach einem Mitgliedschafts-/Sitzungswechsel mit dem alten Entwurf das Fenster schließen und neu öffnen. Entwürfe bleiben innerhalb dieser Spielsitzung erhalten; sie werden nicht dauerhaft als persönliche Einstellung gespeichert.

## Dauerhaft mit dem FDL sprechen

Als TF unten **FDL** anklicken. Der Zugang ist auch dann sichtbar, wenn noch keine FDL-Nachricht vorliegt. Ein Gespräch zur angemeldeten Lok öffnet sich. Jetzt können Freitext, Fahrtanfragen und weitere Bausteine gesendet werden; nach einer Antwort darf erneut gefragt werden.

Ein Host ist im echten Spiel weiterhin berechtigt wie ein Inhaber. Für sein eigenes TF-Gespräch findet er in der FDL-Zugauswahl zusätzlich **TF → FDL-Gespräch**. Für eine vollständig getrennte TF-/FDL-Testansicht gibt es den **Host-Rollentest** aus ROLE-TEST.md.

Neue TF-Fragen erzeugen beim FDL eine persönliche Benachrichtigung. Antworten auf bestehende FDL-Aufforderungen erscheinen beim FDL auch dann als Aktivität, wenn dort keine Lok ausgewählt ist. Der FDL kann die Anfragekarte oder das Gespräch zur Lok öffnen. Freitext-/Bausteinantworten beziehen sich auf die jüngste persönliche offene Anfrage dieses Gesprächs, wenn eine vorhanden ist; anderenfalls wird eine neue Nachricht gesendet.

Im TF-Gespräch ist **Quittieren** verfügbar, sobald eine persönliche offene FDL-Aufforderung vorliegt. Es sendet einmal Verstanden und entfernt diese Antwortmöglichkeit. Das Gespräch bleibt offen. Eine bereits beantwortete TF-Frage muss nicht noch einmal quittiert werden; für eine weitere Bestätigung kann der FDL eine neue Aufforderung senden.

TF sehen den während ihrer eigenen Teilnahme empfangenen Verlauf. Das Wiederöffnen lädt keine alten Archivnachrichten aus der Zeit vor ihrem Beitritt nach. Berechtigte FDL behalten den bisherigen gemeinsamen Übergabeverlauf: bis zu 500 Nachrichten aus sechs echten Stunden, getrennt nach Spielstand.

## Sprachen und Spielwirkung

Übermittelt werden Baustein-ID, geprüfte Werte und ein englischer Rückfalltext. Jeder Empfänger zeichnet Bausteine und feste Aufgaben in seiner eigenen Sprachwahl. Standard ohne erkannte Sprachquelle ist **Englisch**. EnusDE Translation kann die Sprache liefern, bleibt aber optional. Eigene Sprachdateien: Languages/en.json und Languages/de.json. Fehlende Übersetzungen fallen auf Englisch zurück. Ortsnamen, Loknummern, eigener Aufgabentext und frei getippte Chatnachrichten werden nicht automatisch übersetzt.

Fahrtfreigaben und Mannschaftsaufträge sind Chatnachrichten. Sie verändern keine Fahrstraßen, Signale, Weichen oder echte Aufgaben-/Lokzuweisungen. Eine spätere Signalmod-Anbindung ist nicht Bestandteil dieser Fassung.

Für lokale Funktionstests stehen der [Host-Rollentest](ROLE-TEST.md) und die [Leistungsdiagnose](PERFORMANCE.md) zur Verfügung.
