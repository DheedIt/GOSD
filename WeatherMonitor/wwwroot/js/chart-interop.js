window.chartInterop = (() => {
    const _charts = {};

    return {
        create(id, config) {
            // Destroy existing instance to avoid canvas reuse errors
            if (_charts[id]) {
                _charts[id].destroy();
                delete _charts[id];
            }

            const canvas = document.getElementById(id);
            if (!canvas) return;

            _charts[id] = new Chart(canvas.getContext('2d'), config);
        },

        destroy(id) {
            if (_charts[id]) {
                _charts[id].destroy();
                delete _charts[id];
            }
        }
    };
})();
