import { Paper, Typography, Box } from '@mui/material'
import { JSONTree } from 'react-json-tree'

const monokaiTheme = {
  scheme: 'monokai',
  author: 'wimer hazenberg (http://www.monokai.nl)',
  base00: '#272822',
  base01: '#383830',
  base02: '#49483e',
  base03: '#75715e',
  base04: '#a59f85',
  base05: '#f8f8f2',
  base06: '#f5f4f1',
  base07: '#f9f8f5',
  base08: '#f92672',
  base09: '#fd971f',
  base0A: '#f4bf75',
  base0B: '#a6e22e',
  base0C: '#a1efe4',
  base0D: '#66d9ef',
  base0E: '#ae81ff',
  base0F: '#cc6633',
}

interface JsonTreeProps {
  data: unknown
  label?: string
}

export default function JsonTree({ data, label }: JsonTreeProps) {
  if (data === null || data === undefined) {
    return (
      <Paper variant="outlined" sx={{ p: 2 }}>
        <Typography color="text.secondary">No data to display</Typography>
      </Paper>
    )
  }

  return (
    <Box>
      {label && (
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          {label}
        </Typography>
      )}
      <Paper variant="outlined" sx={{ p: 1.5, bgcolor: '#272822', overflow: 'auto' }}>
        <JSONTree
          data={data}
          theme={monokaiTheme}
          invertTheme={false}
          shouldExpandNodeInitially={() => true}
        />
      </Paper>
    </Box>
  )
}
