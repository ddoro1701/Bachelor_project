import React, { useState, useEffect, useCallback } from 'react';
import './App.css';
import EmailSelector from './components/EmailSelector';
import PackageLog from './components/PackageLog';
import MessageHandler from './components/MessageHandler';
import CenterNotice from './components/CenterNotice';
import CameraCapture from './components/CameraCapture';

function App() {
    const [text, setText] = useState('');
    const [cameraOpen, setCameraOpen] = useState(false);
    const [packages, setPackages] = useState([]);

    const uploadFormData = async (formData) => {
      const response = await fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/image/upload', {
        method: 'POST',
        body: formData,
      });
      if (!response.ok) throw new Error('Error uploading image');
      const result = await response.json();
      console.log("OCR Text from /api/image/upload:", result.text);
      setText(result.text);
    };

    const handleImageUpload = async (event) => {
      const file = event.target.files[0];
      if (!file) return;
      const formData = new FormData();
      formData.append('image', file);
      try {
        await uploadFormData(formData);
      } catch (error) {
        console.error('Error:', error);
        setText('Error while uploading File');
      }
    };

    const handleCameraCapture = async (blob) => {
      const formData = new FormData();
      // give it a filename so server treats it like a file
      formData.append('image', blob, 'capture.jpg');
      try {
        await uploadFormData(formData);
      } catch (error) {
        console.error('Error:', error);
        window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'error', message: 'Camera upload failed.' } }));
      }
    };

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
            <CenterNotice />
            <MessageHandler />
            <CameraCapture open={cameraOpen} onClose={() => setCameraOpen(false)} onCapture={handleCameraCapture} />
            <header className="App-header">
                <div className="brand">
                    <h1>Wrexham University Package Management</h1>
                    <p className="subtitle">Scan labels, suggest lecturer, notify instantly</p>
                </div>
                <div style={{ display:'flex', gap:12, flexWrap:'wrap' }}>
                    <label className="file-upload">
                        <span>Upload Label Photo</span>
                        <input
                          type="file"
                          accept="image/*"
                          capture="environment"
                          onChange={handleImageUpload}
                        />
                    </label>
                    <button className="btn" onClick={() => setCameraOpen(true)}>Use Camera</button>
                </div>
            </header>
            <main className="App-main">
                <EmailSelector ocrText={text} fetchPackages={fetchPackages} />
                <PackageLog packages={packages} fetchPackages={fetchPackages} />
            </main>
        </div>
    );
}

export default App;