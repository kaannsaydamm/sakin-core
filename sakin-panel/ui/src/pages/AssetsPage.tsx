import React, { useState, useEffect } from 'react';
import { DataGrid, GridColDef, GridActionsCellItem, GridRowParams } from '@mui/x-data-grid';
import { 
  Button, 
  Dialog, 
  DialogTitle, 
  DialogContent, 
  DialogActions, 
  TextField, 
  FormControl, 
  InputLabel, 
  Select, 
  MenuItem, 
  Chip, 
  Box, 
  Typography, 
  IconButton, 
  Tooltip 
} from '@mui/material';
import { Add, Edit, Delete, Upload, Download } from '@mui/icons-material';

interface Asset {
  id: string;
  name: string;
  ipAddress?: string;
  hostname?: string;
  assetType: string;
  criticality: string;
  owner?: string;
  tags: string[];
  description?: string;
  createdAt: string;
  updatedAt: string;
}

interface AssetFormData {
  name: string;
  ipAddress?: string;
  hostname?: string;
  assetType: string;
  criticality: string;
  owner?: string;
  tags: string;
  description?: string;
}

const assetTypes = ['host', 'service', 'database', 'firewall', 'network_device', 'iot', 'other'];
const criticalityLevels = ['low', 'medium', 'high', 'critical'];

const AssetsPage: React.FC = () => {
  const [assets, setAssets] = useState<Asset[]>([]);
  const [loading, setLoading] = useState(true);
  const [openDialog, setOpenDialog] = useState(false);
  const [editingAsset, setEditingAsset] = useState<Asset | null>(null);
  const [formData, setFormData] = useState<AssetFormData>({
    name: '',
    assetType: 'host',
    criticality: 'medium',
    tags: ''
  });
  const [filters, setFilters] = useState({
    search: '',
    assetType: '',
    criticality: '',
    owner: ''
  });

  const fetchAssets = async () => {
    try {
      const params = new URLSearchParams();
      if (filters.search) params.append('search', filters.search);
      if (filters.assetType) params.append('assetType', filters.assetType);
      if (filters.criticality) params.append('criticality', filters.criticality);
      if (filters.owner) params.append('owner', filters.owner);

      const response = await fetch(`/api/assets?${params}`);
      const data = await response.json();
      setAssets(data.assets || []);
    } catch (error) {
      console.error('Error fetching assets:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAssets();
  }, [filters]);

  const handleOpenDialog = (asset?: Asset) => {
    if (asset) {
      setEditingAsset(asset);
      setFormData({
        name: asset.name,
        ipAddress: asset.ipAddress || '',
        hostname: asset.hostname || '',
        assetType: asset.assetType,
        criticality: asset.criticality,
        owner: asset.owner || '',
        tags: asset.tags.join(', '),
        description: asset.description || ''
      });
    } else {
      setEditingAsset(null);
      setFormData({
        name: '',
        assetType: 'host',
        criticality: 'medium',
        tags: ''
      });
    }
    setOpenDialog(true);
  };

  const handleCloseDialog = () => {
    setOpenDialog(false);
    setEditingAsset(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const payload = {
        ...formData,
        tags: formData.tags ? formData.tags.split(',').map(tag => tag.trim()).filter(Boolean) : []
      };

      const url = editingAsset ? `/api/assets/${editingAsset.id}` : '/api/assets';
      const method = editingAsset ? 'PUT' : 'POST';

      await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      handleCloseDialog();
      fetchAssets();
    } catch (error) {
      console.error('Error saving asset:', error);
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this asset?')) return;

    try {
      await fetch(`/api/assets/${id}`, { method: 'DELETE' });
      fetchAssets();
    } catch (error) {
      console.error('Error deleting asset:', error);
    }
  };

  const handleFileUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('csvFile', file);

    try {
      const response = await fetch('/api/assets/import', {
        method: 'POST',
        body: formData
      });
      
      const result = await response.json();
      alert(`Import completed: ${result.successfulImports}/${result.totalRecords} assets imported successfully`);
      fetchAssets();
    } catch (error) {
      console.error('Error importing assets:', error);
      alert('Error importing assets');
    }
  };

  const downloadTemplate = () => {
    const csvContent = `hostname,ip,asset_type,criticality,owner,tags
DC01,192.168.1.10,host,critical,IT-Core,domain-controller,windows
DB-PROD,192.168.1.20,database,critical,Data-Team,mysql,production`;
    
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'asset_template.csv';
    a.click();
    window.URL.revokeObjectURL(url);
  };

  const columns: GridColDef[] = [
    { field: 'name', headerName: 'Name', width: 150 },
    { field: 'ipAddress', headerName: 'IP Address', width: 140 },
    { field: 'hostname', headerName: 'Hostname', width: 180 },
    { 
      field: 'assetType', 
      headerName: 'Type', 
      width: 120,
      renderCell: (params) => (
        <Chip label={params.value} size="small" variant="outlined" />
      )
    },
    {
      field: 'criticality',
      headerName: 'Criticality',
      width: 120,
      renderCell: (params) => {
        const colors = {
          critical: 'error',
          high: 'warning',
          medium: 'info',
          low: 'default'
        };
        return (
          <Chip 
            label={params.value} 
            size="small" 
            color={colors[params.value as keyof typeof colors] as any}
          />
        );
      }
    },
    { field: 'owner', headerName: 'Owner', width: 120 },
    {
      field: 'tags',
      headerName: 'Tags',
      width: 200,
      renderCell: (params) => (
        <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
          {params.value.slice(0, 3).map((tag: string, index: number) => (
            <Chip key={index} label={tag} size="small" variant="outlined" />
          ))}
          {params.value.length > 3 && (
            <Chip label={`+${params.value.length - 3}`} size="small" variant="outlined" />
          )}
        </Box>
      )
    },
    {
      field: 'actions',
      type: 'actions',
      width: 120,
      getActions: (params: GridRowParams) => [
        <GridActionsCellItem
          icon={<Edit />}
          label="Edit"
          onClick={() => handleOpenDialog(params.row as Asset)}
        />,
        <GridActionsCellItem
          icon={<Delete />}
          label="Delete"
          onClick={() => handleDelete(params.row.id)}
        />
      ]
    }
  ];

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom>
        Asset Management
      </Typography>

      {/* Filters and Actions */}
      <Box sx={{ mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
        <TextField
          label="Search"
          size="small"
          value={filters.search}
          onChange={(e) => setFilters({ ...filters, search: e.target.value })}
          sx={{ minWidth: 200 }}
        />
        
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel>Type</InputLabel>
          <Select
            value={filters.assetType}
            label="Type"
            onChange={(e) => setFilters({ ...filters, assetType: e.target.value })}
          >
            <MenuItem value="">All</MenuItem>
            {assetTypes.map(type => (
              <MenuItem key={type} value={type}>{type}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel>Criticality</InputLabel>
          <Select
            value={filters.criticality}
            label="Criticality"
            onChange={(e) => setFilters({ ...filters, criticality: e.target.value })}
          >
            <MenuItem value="">All</MenuItem>
            {criticalityLevels.map(level => (
              <MenuItem key={level} value={level}>{level}</MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          label="Owner"
          size="small"
          value={filters.owner}
          onChange={(e) => setFilters({ ...filters, owner: e.target.value })}
          sx={{ minWidth: 150 }}
        />

        <Button
          variant="contained"
          startIcon={<Add />}
          onClick={() => handleOpenDialog()}
        >
          Add Asset
        </Button>

        <Button
          variant="outlined"
          startIcon={<Upload />}
          component="label"
        >
          Import CSV
          <input
            type="file"
            accept=".csv"
            hidden
            onChange={handleFileUpload}
          />
        </Button>

        <Tooltip title="Download CSV Template">
          <IconButton onClick={downloadTemplate}>
            <Download />
          </IconButton>
        </Tooltip>
      </Box>

      {/* Data Grid */}
      <Box sx={{ height: 600, width: '100%' }}>
        <DataGrid
          rows={assets}
          columns={columns}
          loading={loading}
          getRowId={(row) => row.id}
          initialState={{
            pagination: {
              paginationModel: { page: 0, pageSize: 20 },
            },
          }}
          pageSizeOptions={[10, 20, 50]}
        />
      </Box>

      {/* Add/Edit Dialog */}
      <Dialog open={openDialog} onClose={handleCloseDialog} maxWidth="md" fullWidth>
        <DialogTitle>{editingAsset ? 'Edit Asset' : 'Add Asset'}</DialogTitle>
        <form onSubmit={handleSubmit}>
          <DialogContent>
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
              <TextField
                label="Name"
                required
                fullWidth
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
              />
              
              <TextField
                label="IP Address"
                fullWidth
                value={formData.ipAddress}
                onChange={(e) => setFormData({ ...formData, ipAddress: e.target.value })}
              />
              
              <TextField
                label="Hostname"
                fullWidth
                value={formData.hostname}
                onChange={(e) => setFormData({ ...formData, hostname: e.target.value })}
              />
              
              <FormControl fullWidth required>
                <InputLabel>Asset Type</InputLabel>
                <Select
                  value={formData.assetType}
                  label="Asset Type"
                  onChange={(e) => setFormData({ ...formData, assetType: e.target.value })}
                >
                  {assetTypes.map(type => (
                    <MenuItem key={type} value={type}>{type}</MenuItem>
                  ))}
                </Select>
              </FormControl>
              
              <FormControl fullWidth required>
                <InputLabel>Criticality</InputLabel>
                <Select
                  value={formData.criticality}
                  label="Criticality"
                  onChange={(e) => setFormData({ ...formData, criticality: e.target.value })}
                >
                  {criticalityLevels.map(level => (
                    <MenuItem key={level} value={level}>{level}</MenuItem>
                  ))}
                </Select>
              </FormControl>
              
              <TextField
                label="Owner"
                fullWidth
                value={formData.owner}
                onChange={(e) => setFormData({ ...formData, owner: e.target.value })}
              />
              
              <TextField
                label="Tags (comma-separated)"
                fullWidth
                value={formData.tags}
                onChange={(e) => setFormData({ ...formData, tags: e.target.value })}
                helperText="Enter tags separated by commas"
              />
              
              <TextField
                label="Description"
                fullWidth
                multiline
                rows={3}
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
              />
            </Box>
          </DialogContent>
          <DialogActions>
            <Button onClick={handleCloseDialog}>Cancel</Button>
            <Button type="submit" variant="contained">
              {editingAsset ? 'Update' : 'Create'}
            </Button>
          </DialogActions>
        </form>
      </Dialog>
    </Box>
  );
};

export default AssetsPage;