import './polyfills/iterable-dom.js';
import trackFeature from './helpers/track-feature.js';
import languages from './helpers/languages.js';
import targets from './helpers/targets.js';
import getBranchesAsync from './server/get-branches-async.js';
import state from './state/index.js';
import url from './state/handlers/url.js';
import defaults from './state/handlers/defaults.js';
import uiAsync from './ui/index.js';

/* eslint-disable no-invalid-this */

function getResultType(target) {
    switch (target) {
        case targets.verify: return 'verify';
        case targets.ast: return 'ast';
        case targets.run: return 'run';
        default: return 'code';
    }
}

function resetLoading() {
    if (this.loadingDelay) {
        clearTimeout(this.loadingDelay);
        this.loadingDelay = null;
    }
    this.loading = false;
}

function applyUpdateWait() {
    if (this.loadingDelay)
        return;

    this.loadingDelay = setTimeout(() => {
        this.loading = true;
        this.loadingDelay = null;
    }, 300);
}

function applyUpdateResult(updateResult) {
    const result = {
        success: true,
        type: getResultType(this.options.target),
        value: updateResult.x,
        errors: [],
        warnings: []
    };
    for (const diagnostic of updateResult.diagnostics) {
        if (diagnostic.severity === 'error') {
            if (result.type !== 'ast')
                result.success = false;
            result.errors.push(diagnostic);
        }
        else if (diagnostic.severity === 'warning') {
            result.warnings.push(diagnostic);
        }
    }
    this.result = result;
    this.lastResultOfType[result.type] = result;
    resetLoading.apply(this);
}

function applyServerError(message) {
    this.result = {
        success: false,
        errors: [{ message }],
        warnings: []
    };
    resetLoading.apply(this);
}

function applyConnectionChange(connectionState) {
    this.online = (connectionState === 'open');
}

function getServiceUrl(branch) {
    const httpRoot = branch ? branch.url : window.location.origin;
    return `${httpRoot.replace(/^http/, 'ws')}/mirrorsharp`;
}

function applyAstHover(item) {
    if (!item || !item.range) {
        this.highlightedCodeRange = null;
        return;
    }
    const [start, end] = item.range.split('-');
    this.highlightedCodeRange = { start, end };
}

async function createAppAsync() {
    const data = Object.assign({
        languages,
        targets,

        branchGroups: [],
        branch: null,

        online: true,
        loading: true,

        result: {
            success: true,
            type: '',
            value: '',
            errors: [],
            warnings: []
        },
        lastResultOfType: { run: null, code: null, ast: null },

        highlightedCodeRange: null
    });
    await state.loadAsync(data);
    data.lastLoadedCode = data.code;

    const branchesPromise = (async () => {
        const branches = await getBranchesAsync();
        const groups = {};
        for (const branch of branches) {
            let group = groups[branch.group];
            if (!group) {
                group = { name: branch.group, branches: [] };
                groups[branch.group] = group;
                data.branchGroups.push(group);
            }
            group.branches.push(branch);
        }
        return branches;
    })();

    if (data.options.branchId) {
        const branches = await branchesPromise;
        data.branch = branches.filter(b => b.id === data.options.branchId)[0];
    }
    data.serviceUrl = getServiceUrl(data.branch);

    return {
        data,
        computed: {
            serverOptions() {
                return {
                    language: this.options.language,
                    'x-optimize': this.options.release ? 'release' : 'debug',
                    'x-target': this.options.target
                };
            },
            status() {
                if (!this.online)
                    return { name: 'offline', color: '#aaa' };
                if (!this.result.success)
                    return { name: 'error', color: '#dc3912' };
                return { name: 'default', color: '#4684ee' };
            }
        },
        methods: { applyUpdateWait, applyUpdateResult, applyServerError, applyConnectionChange, applyAstHover }
    };
}

(async function runAsync() {
    const app = await createAppAsync();
    const ui = await uiAsync(app);
    const data = app.data;

    ui.watch('options', () => state.save(data), { deep: true });
    ui.watch('code', () => state.save(data));
    ui.watch('branch', value => {
        data.options.branchId = value ? value.id : null;
        if (value)
            trackFeature('Branch: ' + value.id);
        data.loading = true;
        data.serviceUrl = getServiceUrl(value);
    });

    ui.watch('options.language', (newLanguage, oldLanguage) => {
        trackFeature('Language: ' + newLanguage);
        if (newLanguage === languages.fsharp)
            data.branch = null;

        const target = data.options.target;
        if (data.code !== defaults.getCode(oldLanguage, target))
            return;
        data.code = defaults.getCode(newLanguage, target);
        data.lastLoadedCode = data.code;
    });

    ui.watch('options.target', (newTarget, oldTarget) => {
        trackFeature('Target: ' + newTarget);
        const language = data.options.language;
        if (data.code !== defaults.getCode(language, oldTarget))
            return;
        data.code = defaults.getCode(language, newTarget);
        data.lastLoadedCode = data.code;
    });

    url.changed(async () => {
        await state.loadAsync(data);
        data.lastLoadedCode = data.code;
    });
})();