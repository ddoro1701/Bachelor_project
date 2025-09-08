import React, { useState, useEffect, useCallback } from 'react';
import './App.css'; // Neue Styles laden
import EmailSelector from './components/EmailSelector';
import PackageLog from './components/PackageLog';

function App() {
    const [text, setText] = useState('');

    const handleImageUpload = async (event) => {
        const file = event.target.files[0];
        if (!file) return;
      
        const formData = new FormData();
        formData.append('image', file);
      
        try {
          const response = await fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/image/upload', {
            method: 'POST',
            body: formData,
          });
      
          if (!response.ok) {
            throw new Error('Error uploading image');
          }
      
          const result = await response.json();
          console.log("OCR Text from /api/image/upload:", result.text);
          setText(result.text);
        } catch (error) {
          console.error('Error:', error);
          setText('Error while uploading File');
        }
      };

    const [packages, setPackages] = useState([]);

    const fetchPackages = useCallback(() => {
        fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/package/all')
            .then(res => res.json())
            .then(data => {
                setPackages(data);
            })
            .catch(err => console.error('Error fetching packages:', err));
    }, []);

    useEffect(() => {
        fetchPackages();
    }, [fetchPackages]);

    return (
        <div className="App">
            <header className="App-header">
                <div className="brand">
                    <h1>Wrexham University Package Management</h1>
                    <p className="subtitle">Scan labels, suggest lecturer, notify instantly</p>
                </div>

                <label className="file-upload">
                    <span>Upload Label Photo</span>
                    <input type="file" accept="image/*" onChange={handleImageUpload} />
                </label>
            </header>

            <main className="App-main">
                <EmailSelector ocrText={text} fetchPackages={fetchPackages} />
                <PackageLog packages={packages} fetchPackages={fetchPackages} />
            </main>
        </div>
    );
}

export default App;