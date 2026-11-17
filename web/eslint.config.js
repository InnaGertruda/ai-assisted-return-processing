import eslint from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'

export default [
  { ignores: ['dist', 'coverage'] },
  eslint.configs.recommended,
  ...pluginVue.configs['flat/essential'],
]
