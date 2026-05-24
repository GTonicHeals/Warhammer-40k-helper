window.campaignStorage = {
    load: function (key) { return localStorage.getItem(key); },
    save: function (key, json) { localStorage.setItem(key, json); }
};
