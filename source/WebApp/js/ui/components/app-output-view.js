import Vue from 'vue';

Vue.component('app-output-view', {
    props: {
        output: Array
    },
    methods: {
        isMultiline(inspection) {
            return /[\r\n]/.test(inspection.value);
        },

        isException(inspection) {
            return inspection.title === 'Exception';
        },

        isWarning(inspection) {
            return inspection.title === 'Warning';
        }
    },
    template: `<div class="output result-content">
      <small class="output-disclaimer">
        This is a new feature — might be unstable/too strict. Please <a href="https://github.com/ashmind/SharpLab/issues">report</a> any issues.
      </small>
      <div class="output-empty" v-show="output.length === 0">Completed — no output.</div>
      <template v-for="item in output">
        <pre v-if="typeof item === 'string'">{{item}}</pre>
        <div v-if="item.type === 'inspection'"
             class="inspection"
             v-bind:class="{ 'inspection-multiline': isMultiline(item), 'inspection-exception': isException(item), 'inspection-warning': isWarning(item) }">
          <header>{{item.title}}</header>
          <div class="inspection-value">{{item.value}}</div>
        </div>
      </template>
    </div>`.replace(/[\r\n]+\s*/g, '').replace(/\s{2,}/g, ' ')
});