import { Button } from 'npm-lib'

export function App() {
  const handleClick = () => {
    // your logic here
    console.log('clicked')
  }

  return (
    <div style={{ padding: 16 }}>
      <h1>Web App</h1>
      <Button onClick={handleClick}>
        Hello from ui-kit
      </Button>
    </div>
  )
}