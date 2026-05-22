window.crusadeStorage = {
    getAll: function () {
        const result = {};
        for (let i = 0; i < localStorage.length; i++) {
            const k = localStorage.key(i);
            if (k && k.startsWith("crusade_")) {
                result[k] = localStorage.getItem(k);
            }
        }
        return result;
    },
    save: function (key, json) { localStorage.setItem(key, json); },
    remove: function (key) { localStorage.removeItem(key); }
};
