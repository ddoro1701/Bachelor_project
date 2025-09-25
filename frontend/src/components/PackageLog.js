import React, { useEffect } from 'react';
import LogTable from './LogTable';

const PackageLog = ({ packages, fetchPackages }) => {
    const handleToggleStatus = (packageItem) => {
        const updatedStatus = packageItem.status === 'Received' ? 'Collected' : 'Received';
        fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/package/update-status', {
            method: 'POST', // use POST like other endpoints
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
            },
            body: JSON.stringify({ id: packageItem.id, status: updatedStatus }),
        })
            .then(async (res) => {
                if (!res.ok) {
                    const msg = await res.text();
                    throw new Error(msg || 'Failed to update status');
                }
                const text = await res.text();
                return text ? JSON.parse(text) : null;
            })
            .then(() => {
                fetchPackages();
            })
            .catch(err => console.error('Error updating status:', err));
    };

    const handleDeleteCollected = (deletedIds) => {
        fetchPackages(); // Refresh the package list after deletion
    };

    useEffect(() => {
        fetchPackages();
    }, [fetchPackages]);

    return (
        <div>
            <h1>Package Log</h1>
            <LogTable
                data={packages}
                onToggleStatus={handleToggleStatus}
                onDeleteCollected={handleDeleteCollected}
            />
        </div>
    );
};

export default PackageLog;