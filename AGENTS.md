# Project working rules

GitHub repository `EnusDE/EnusDE-Communications` is the shared source for local and Cloud work. Check the branch, HEAD and working-tree changes before editing. Preserve unfinished work when switching branches. Communicate the branch and commit when handing work to the other chat.

The current target is Railroader 0.6.3. Build and test on the Windows PC against its installed game using Build-Local.cmd or the explicit GameDirectory parameter. Cloud can edit source and run suitable isolated tests. Report which checks actually ran and which live game tests remain open.

EnusDE Translation is optional. English is the fallback. Language detection must avoid repeated global type enumeration when the optional mod is missing. Retain detection of late-loaded or reactivated translators and remove event subscriptions on mod unload.

Retain native game settings, optional multiplayer participation, the train-only hold-to-select F10 wheel, and the existing crew and dispatcher permission boundaries. Preserve default-off voice consent. Keep the newer voice work on its own branch until intentionally integrated.

Keep personal settings, game DLLs, game saves, logs, build outputs, private paths and internal notes outside version control and release assets. Include Harmony's license notices. The README is English first with a short German summary. Release notes are English and German. The user publishes releases after reviewing the local test build.
