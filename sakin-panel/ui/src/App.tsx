import './App.css';
import { AlertList } from './components/AlertList';

function App() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <h1>Sakin Panel</h1>
        <p className="subtitle">
          Monitor incoming security alerts and acknowledge incidents as you review
          them.
        </p>
      </header>

      <main className="app-main">
        <AlertList pageSize={10} />
      </main>
    </div>
  );
}

export default App;
