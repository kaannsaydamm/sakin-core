import './App.css';
import { BrowserRouter as Router, Routes, Route, Link, useLocation } from 'react-router-dom';
import { AlertList } from './components/AlertList';
import AssetsPage from './pages/AssetsPage';
import { 
  AppBar, 
  Toolbar, 
  Typography, 
  Button, 
  Box, 
  Container 
} from '@mui/material';

function Navigation() {
  const location = useLocation();
  
  return (
    <AppBar position="static">
      <Toolbar>
        <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
          Sakin Panel
        </Typography>
        <Button 
          color="inherit" 
          component={Link} 
          to="/"
          sx={{ mr: 1 }}
          variant={location.pathname === '/' ? 'outlined' : 'text'}
        >
          Alerts
        </Button>
        <Button 
          color="inherit" 
          component={Link} 
          to="/assets"
          variant={location.pathname === '/assets' ? 'outlined' : 'text'}
        >
          Assets
        </Button>
      </Toolbar>
    </AppBar>
  );
}

function App() {
  return (
    <Router>
      <Box sx={{ flexGrow: 1 }}>
        <Navigation />
        <Container maxWidth="xl" sx={{ mt: 3, mb: 3 }}>
          <Routes>
            <Route 
              path="/" 
              element={
                <div>
                  <Typography variant="h4" gutterBottom>
                    Security Alerts
                  </Typography>
                  <Typography variant="body1" color="text.secondary" gutterBottom>
                    Monitor incoming security alerts and acknowledge incidents as you review them.
                  </Typography>
                  <AlertList pageSize={25} />
                </div>
              } 
            />
            <Route path="/assets" element={<AssetsPage />} />
          </Routes>
        </Container>
      </Box>
    </Router>
  );
}

export default App;
