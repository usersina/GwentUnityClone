// In a browser, files live in memory and reach the browser's storage (IndexedDB) only when synced: DeckStorage asks
// for it after saving or deleting a deck, or the decks would be gone on the next visit
mergeInto(LibraryManager.library, {
  GwentSyncFiles: function () {
    FS.syncfs(false, function (error) {
      if (error)
        console.warn("Gwent: the decks could not be stored in the browser: " + error);
    });
  }
});
